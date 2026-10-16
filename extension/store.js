// Capture storage in IndexedDB. Shared by the service worker and the extension pages (same origin),
// so captures survive a service-worker restart and images never travel through runtime messages.
//
// Record: { id, createdAt, errors: string[], images: [{ blob, mime, ext, name, width, height, size, title }] }

const skbStore = (() => {
  const DB_NAME = 'imageshot';
  const STORE = 'captures';
  let dbPromise = null;

  function open() {
    if (dbPromise) return dbPromise;
    dbPromise = new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, 1);
      req.onupgradeneeded = () => {
        const os = req.result.createObjectStore(STORE, { keyPath: 'id' });
        os.createIndex('createdAt', 'createdAt');
      };
      req.onsuccess = () => {
        const db = req.result;
        db.onversionchange = () => { db.close(); dbPromise = null; };
        db.onclose = () => { dbPromise = null; };
        resolve(db);
      };
      req.onerror = () => { dbPromise = null; reject(req.error); };
      req.onblocked = () => { dbPromise = null; reject(new Error('IndexedDB is blocked')); };
    });
    return dbPromise;
  }

  async function tx(mode, work) {
    const db = await open();
    return new Promise((resolve, reject) => {
      const t = db.transaction(STORE, mode);
      let result;
      const req = work(t.objectStore(STORE));
      if (req) req.onsuccess = () => { result = req.result; };
      t.oncomplete = () => resolve(result);
      t.onerror = () => reject(t.error);
      t.onabort = () => reject(t.error || new Error('IndexedDB transaction aborted'));
    });
  }

  return {
    put: record => tx('readwrite', s => s.put({ createdAt: Date.now(), errors: [], ...record })),
    get: id => tx('readonly', s => s.get(id)),
    remove: id => tx('readwrite', s => s.delete(id)),
    ids: () => tx('readonly', s => s.getAllKeys()),
    // Deletes records created more than `maxAgeMs` ago; resolves with the number removed.
    purgeOlderThan(maxAgeMs) {
      let removed = 0;
      return tx('readwrite', s => {
        const range = IDBKeyRange.upperBound(Date.now() - maxAgeMs, true);
        const cur = s.index('createdAt').openCursor(range);
        cur.onsuccess = () => {
          const c = cur.result;
          if (!c) return;
          c.delete();
          removed++;
          c.continue();
        };
      }).then(() => removed);
    }
  };
})();
