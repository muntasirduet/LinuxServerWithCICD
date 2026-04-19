# AWS EC2 (Amazon Linux) + Nginx + RDS + CI/CD Setup (Free Tier) — Line by Line

This guide is a **step-by-step** setup for running this project on **AWS Free Tier** using:

- EC2 (Amazon Linux 2023)
- RDS (SQL Server Express, Free Tier eligible)
- Nginx
- GitHub Actions CI/CD

---

## 1) Create AWS resources (Free Tier safe)

### Step 1.1 — Launch EC2
1. Open AWS Console → **EC2** → **Launch instance**.
2. Name: `myapp-ec2`.
3. AMI: **Amazon Linux 2023**.
4. Instance type: **t2.micro** (Free Tier default).  
   In regions/accounts where t2.micro is unavailable, AWS Free Tier may allow **t3.micro**.
5. Key pair: create/download a `.pem` key.
6. Network:
   - Auto-assign public IP: **Enable**
   - Security group inbound:
     - SSH `22` from **My IP**
     - HTTP `80` from `0.0.0.0/0`
     - HTTPS `443` from `0.0.0.0/0`
7. Storage: keep default (Free Tier limit).
8. Click **Launch instance**.

### Step 1.2 — Create RDS (Free Tier)
1. Open AWS Console → **RDS** → **Create database**.
2. Engine: **Microsoft SQL Server**.
3. Edition: **SQL Server Express Edition**.
4. Template: **Free tier**.
5. DB instance identifier: `myappdb`.
6. Master username: `admin`.
7. Set a strong master password and save it.
8. DB instance class: **db.t2.micro** (Free Tier safe default).
9. Storage: default (Free Tier value).
10. Connectivity:
    - VPC: same VPC as EC2
    - Public access: **No**
11. VPC security group:
    - Allow inbound `1433` from the EC2 security group only.
12. Create database.
13. Wait until status is **Available**.
14. Copy the **RDS endpoint**.

---

## 2) Connect to EC2 and install software

### Step 2.1 — SSH to EC2
```bash
chmod 400 your-key.pem
ssh -i your-key.pem ec2-user@YOUR_EC2_PUBLIC_IP
```

### Step 2.2 — Update packages
```bash
sudo dnf update -y
sudo dnf install -y ca-certificates
sudo update-ca-trust
```

### Step 2.3 — Install .NET 8 SDK + runtime
```bash
sudo rpm -Uvh https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm
sudo dnf install -y dotnet-sdk-8.0 aspnetcore-runtime-8.0
dotnet --info
```

### Step 2.4 — Install Nginx, Git, rsync
```bash
sudo dnf install -y nginx git rsync
sudo systemctl enable nginx
sudo systemctl start nginx
```

---

## 3) Prepare server folders and service user

```bash
sudo useradd -r -m -s /bin/bash myapp || true
sudo mkdir -p /var/www/myapp /var/log/myapp /opt/src
sudo chown -R ec2-user:ec2-user /opt/src
sudo chown -R myapp:myapp /var/www/myapp /var/log/myapp
```

---

## 4) Build and publish app on EC2 (first time)

```bash
cd /opt/src
git clone https://github.com/YOUR_USERNAME/YOUR_REPO.git myapp
cd /opt/src/myapp

dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/MyApp.Api -c Release -o ./publish --no-build

sudo rsync -av --delete ./publish/ /var/www/myapp/
sudo chown -R myapp:myapp /var/www/myapp
```

> Replace the clone URL with your own repository/fork URL.
> If your API project path is not `src/MyApp.Api`, update the publish command path accordingly.

---

## 5) Configure app secrets for RDS

Create systemd override file:

```bash
sudo mkdir -p /etc/systemd/system/myapp.service.d
sudo tee /etc/systemd/system/myapp.service.d/override.conf >/dev/null <<'EOF'
[Service]
Environment="ConnectionStrings__Default=Server=YOUR_RDS_ENDPOINT,1433;Database=MyAppDb;User Id=admin;Password=YOUR_RDS_PASSWORD;Encrypt=True;TrustServerCertificate=False"
Environment="Jwt__Key=REPLACE_WITH_STRONG_32BYTE_BASE64_KEY"
Environment="ASPNETCORE_ENVIRONMENT=Production"
EOF
sudo chmod 600 /etc/systemd/system/myapp.service.d/override.conf
sudo systemctl daemon-reload
```

Generate JWT key example:
```bash
openssl rand -base64 32
```

> For stronger security, store DB/JWT secrets in **AWS Secrets Manager** or **SSM Parameter Store** and inject them at deploy/runtime.  
> Keep `TrustServerCertificate=False` in production and ensure system CA trust is up to date for RDS certificate validation.
> Install/update trust store (`ca-certificates`) and follow AWS RDS CA certificate guidance: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/UsingWithRDS.SSL.html

---

## 6) Configure systemd service

```bash
sudo cp /opt/src/myapp/deploy/linux/myapp.service /etc/systemd/system/myapp.service
sudo systemctl daemon-reload
sudo systemctl enable myapp
sudo systemctl start myapp
sudo systemctl status myapp --no-pager
```

---

## 7) Configure Nginx reverse proxy

```bash
sudo cp /opt/src/myapp/deploy/nginx/myapp.conf /etc/nginx/conf.d/myapp.conf
sudo nginx -t
sudo systemctl restart nginx
```

Test:
```bash
curl -I http://localhost:5000/health
curl -I http://YOUR_EC2_PUBLIC_IP/health
```

---

## 8) Configure GitHub Actions CI/CD

This repository already has workflow file: `.github/workflows/deploy.yml`.

### Step 8.1 — Add GitHub repository secrets
Go to GitHub repo → **Settings** → **Secrets and variables** → **Actions**:

- `SERVER_IP` = EC2 public IP
- `SERVER_USER` = `ec2-user`
- `SSH_PRIVATE_KEY` = private key used for deployment

### Step 8.2 — Add deploy public key to EC2
On your local machine:
```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ./github-actions-deploy-key
```

Add public key to server:
```bash
cat github-actions-deploy-key.pub
```
Copy output, then on EC2:
```bash
mkdir -p ~/.ssh
chmod 700 ~/.ssh
nano ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```
Paste the public key on a new line, then save and exit.

Set GitHub secret `SSH_PRIVATE_KEY` to content of `github-actions-deploy-key`.

### Step 8.3 — Trigger CI/CD
1. Push code to `main`.
2. Open GitHub **Actions** tab.
3. Confirm workflow success.
4. Verify app:
   ```bash
   curl -I http://YOUR_EC2_PUBLIC_IP/health
   ```

---

## 9) Optional HTTPS (recommended)

If domain is configured:

```bash
sudo dnf install -y certbot python3-certbot-nginx
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com
sudo certbot renew --dry-run
```

---

## 10) Free Tier checklist

- Use only **1 EC2 t2.micro** (or only free-tier-eligible type in your account).
- Use only **1 RDS db.t2.micro SQL Server Express**.
- Keep storage within free limits.
- Stop/delete unused resources.
- Monitor billing in AWS Billing Dashboard.

---

## 11) Quick troubleshooting

```bash
sudo systemctl status myapp nginx --no-pager
sudo journalctl -u myapp -n 200 --no-pager
sudo nginx -t
curl -I http://localhost:5000/health
```
