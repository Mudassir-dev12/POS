# ShopPOS Store Setup & GitHub Backup Card

Keep this document in a safe physical location (e.g. store safe or owner binder).

---

## 1. Store & Ownership Details
- **Store Name:** [Store Name]
- **Store Owner:** [Owner Full Name]
- **Primary Contact Number:** [Phone Number]
- **Emergency / Recovery Contact:** [Support Tech / Contact Person & Phone]

---

## 2. GitHub Backup Repository Details
- **GitHub Account / Org:** [e.g. your-store-backups]
- **Repository Name:** [e.g. shop-pos-backups] (Must be **PRIVATE**)
- **Branch:** `main`
- **Releases URL:** `https://github.com/[owner]/[repo]/releases`
- **Fine-Grained Personal Access Token (PAT):**
  - Stored encrypted in Windows DPAPI on the POS machine (`%LOCALAPPDATA%\ShopPOS\github_token.dat`).
  - Permissions required: **Repository Permissions > Contents: Read and write**.
  - Token Expiration Date: [Date to renew token]

---

## 3. Backup Encryption Password
> **CRITICAL WARNING:** Backups are encrypted with AES-256-GCM. If this password is lost, data **CANNOT** be recovered by any party or technician.
- **Master Encryption Password:** `________________________________________`
- **Password Location / Safe Notes:** [Where physical or password manager copy is stored]

---

## 4. Release Assets & Year-End Schedule
- **Yearly Release Tag:** `backup-<yyyy>` (e.g. `backup-2026`).
- **Daily Assets:** `ShopPOS_<slug>_<yyyyMMdd-HHmmss>.zip.enc` (newest 30 days retained on GitHub).
- **Monthly Assets:** `ShopPOS_<slug>_<yyyy-MM>_monthly.zip.enc` (permanent, first successful upload of the month).
- **Year-End Archive:** `ShopPOS_<slug>_<yyyy>_YEAR-END.zip.enc` (permanent, contains database snapshot plus encrypted CSV exports for sales, purchases, expenses, and inventory).

---

## 5. Routine Checks
1. Check the POS Dashboard weekly: verify that the GitHub Backup status displays "Connected" and no warning banners appear.
2. If token expiration is approaching, generate a new token in GitHub settings and update it via **Backup & Restore > GitHub Settings > Save GitHub Settings**.

