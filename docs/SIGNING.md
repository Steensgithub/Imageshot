# Kodesignering (forberedt – slås til, når du beslutter det)

Uden signering viser Windows SmartScreen "Windows beskyttede din pc", første gang nogen kører
installationsprogrammet. Med signering forsvinder advarslen (gradvist, efterhånden som filen får omdømme).

Udgivelses-workflowet (`.github/workflows/release.yml`) signerer **automatisk**, så snart disse
hemmeligheder findes i arkivet (Settings → Secrets and variables → Actions). Uden dem bygges der usigneret.

| Hemmelighed | Indhold |
|---|---|
| `AZURE_TENANT_ID` | Microsoft Entra-lejer (tenant) |
| `AZURE_CLIENT_ID` | App-registreringens klient-id |
| `AZURE_CLIENT_SECRET` | App-registreringens hemmelighed |
| `AZURE_SIGNING_ENDPOINT` | fx `https://weu.codesigning.azure.net/` |
| `AZURE_SIGNING_ACCOUNT` | Navnet på Trusted Signing-kontoen |
| `AZURE_SIGNING_PROFILE` | Navnet på certifikatprofilen |

## Sådan kommer du i gang med Azure Trusted Signing (ca. 10 USD/md.)
1. Opret et Azure-abonnement og ressourcen **Trusted Signing Account** (region fx West Europe).
2. Gennemfør identitetsbekræftelsen (*Identity validation*, privatperson eller firma).
3. Opret en **Certificate profile** af typen *Public Trust*.
4. Opret en app-registrering i Entra ID, giv den rollen *Trusted Signing Certificate Profile Signer* på kontoen.
5. Læg de seks værdier ind som hemmeligheder (se tabellen).

Alternativ: et almindeligt OV/EV-kodesigneringscertifikat. Så udskiftes de to `azure/trusted-signing-action`-trin
med `signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 …`.
