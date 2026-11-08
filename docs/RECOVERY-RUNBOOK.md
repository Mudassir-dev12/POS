# ShopPOS Emergency Disaster Recovery Runbook

This guide instructs support technicians or owners on recovering store data from private GitHub Release Assets onto a new or repaired Windows PC.

---

## Security Model Notice
- Support accounts with collaborator access to the backup repository can only view and download **encrypted `.zip.enc` assets**.
- Support staff **CANNOT** read or decrypt sales, financials, or customer records without the owner's master encryption password.
- No business data is ever stored in git commits or unencrypted metadata.

---

## Scenario A: Recovery in Clean Setup Wizard (New Machine)
Use this procedure when installing ShopPOS on a brand-new or formatted Windows machine.

1. **Install ShopPOS**:
   - Run `ShopPOS-Setup-<version>.exe`. Complete the installer.
2. **Launch Setup Wizard**:
   - On first launch, the Setup Wizard Welcome screen appears.
   - Select option: **Restore from an existing backup**.
   - Click **Restore from GitHub...**.
3. **Connect to GitHub**:
   - Enter Repository Owner, Repository Name, Branch, and Personal Access Token (or Read-only Support PAT).
   - Click **Refresh**.
4. **Select Snapshot**:
   - Choose the desired release asset from the list:
     - **Latest Daily Asset** for nearest recovery point.
     - **Monthly Asset** (`*_monthly.zip.enc`) for month-boundary restore.
     - **Year-End Archive** (`*_YEAR-END.zip.enc`) for historical audit restore.
5. **Download & Decrypt**:
   - Click **Restore Selected Backup**.
   - The asset and its SHA-256 manifest will download and verify.
   - Enter the store's **Master Encryption Password** when prompted.
   - Click **Confirm Restore**.
   - ShopPOS will unpack the database and launch into production.

---

## Scenario B: Manual Browser Download & Restore
If the store machine is offline or internet access is on a different machine:

1. **Sign in to GitHub**:
   - Open a web browser and go to `https://github.com/<owner>/<repo>/releases`.
2. **Download Target Assets**:
   - Locate the target release (e.g. `backup-2026`).
   - Download the target `.zip.enc` file (e.g. `ShopPOS_MyShop_20261107-120000.zip.enc`).
   - Download its companion manifest: `ShopPOS_MyShop_20261107-120000.zip.enc.manifest.json`.
3. **Transfer to Store PC**:
   - Copy the files to a USB drive and plug it into the store PC.
4. **Restore via ShopPOS UI**:
   - In ShopPOS, open **Backup & Restore** > click **Restore...**.
   - Browse to the USB drive and select the `.zip.enc` file.
   - Enter the store's **Master Encryption Password**.
   - Review the confirmation dialog and click **Restore Database**.

---

## Scenario C: Copy / Migrate Backups to a New Repository
When transferring backups from a temporary technician repository or migrating to a new company account:

1. Open **Backup & Restore** > **GitHub Settings**.
2. Click **Copy to Another Repo...**.
3. Select Source:
   - **From Another GitHub Repository**: Provide source owner, repo, and source token.
   - **From Local Folder**: Provide path to local directory with downloaded `ShopPOS_*` files.
4. Provide Destination details:
   - Destination Owner, Repository, and Token with `Contents: write` permission.
5. Click **Start Copying**.
   - ShopPOS verifies each asset against its manifest, replicates the yearly release tags (`backup-<yyyy>`), and skips identical existing assets. Source data is never modified or deleted.

---

## Troubleshooting & FAQ
- **Checksum Mismatch Error:** If SHA-256 validation fails during download, the file was truncated or corrupted during transmission. Re-download the asset.
- **Wrong Password:** If password decryption fails, check Caps Lock. Due to PBKDF2 + AES-256-GCM authenticated encryption, an incorrect password immediately aborts before any database touch.
- **Asset Size Limits:** Release assets support files up to 2 GiB. ShopPOS enforces an internal safety warning at 500 MB and a hard cap at 1.8 GiB.

