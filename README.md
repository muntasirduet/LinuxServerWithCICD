# MyApp — .NET 8 Web API on AWS EC2 (Amazon Linux 2) with CI/CD

This repository contains a production-oriented .NET 8 Web API setup with:

- ASP.NET Core Web API
- PostgreSQL via EF Core
- JWT Bearer authentication
- Serilog JSON console logging
- Nginx reverse proxy and systemd service templates
- GitHub Actions build/test/deploy workflow

## Project structure

- `src/MyApp.Api` — API startup, controllers, configuration
- `src/MyApp.Core` — core entities and business services
- `src/MyApp.Infrastructure` — EF Core DbContext and repositories
- `tests/MyApp.Tests` — unit tests
- `deploy/linux/myapp.service` — systemd service template
- `deploy/nginx/myapp.conf` — Nginx site config template
- `.github/workflows/deploy.yml` — CI/CD pipeline

## Local commands

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/MyApp.Api
```

---

## AWS EC2 + Amazon Linux 2 Setup Guide (Detailed)

## 1) EC2 instance setup

### Recommended instance types
- **Dev/Test:** `t3.small` (2 vCPU, 2 GB RAM)
- **Small production:** `t3.medium` (2 vCPU, 4 GB RAM)
- **Higher traffic:** `t3.large` or `m6i.large`

### Security group (minimum)
- Inbound:
  - `22/tcp` from your office/home IP only (SSH)
  - `80/tcp` from `0.0.0.0/0` (HTTP)
  - `443/tcp` from `0.0.0.0/0` (HTTPS)
- Outbound:
  - Allow all (default)

### Network and storage
- Use a public subnet + Internet Gateway.
- Assign Elastic IP to avoid IP changes.
- Root EBS volume: at least **20 GB gp3**.
- Keep PostgreSQL local only (do not expose port 5432 publicly).

### SSH key pair
- Create/download a `.pem` key pair at EC2 launch.
- Set permissions locally:
  ```bash
  chmod 400 my-key.pem
  ssh -i my-key.pem ec2-user@<EC2_PUBLIC_IP>
  ```

## 2) Amazon Linux 2 environment configuration

SSH into the server and run:

```bash
sudo yum update -y
sudo yum install -y yum-utils git
```

### Install .NET 8 SDK/runtime
```bash
sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
sudo yum install -y dotnet-sdk-8.0 aspnetcore-runtime-8.0
dotnet --info
```

### Install PostgreSQL
```bash
sudo amazon-linux-extras enable postgresql14
sudo yum clean metadata
sudo yum install -y postgresql postgresql-server
sudo postgresql-setup initdb
sudo systemctl enable postgresql
sudo systemctl start postgresql
```

### Install Nginx
```bash
sudo amazon-linux-extras enable nginx1
sudo yum clean metadata
sudo yum install -y nginx
sudo systemctl enable nginx
sudo systemctl start nginx
```

### Install Node.js and Git
```bash
sudo amazon-linux-extras enable nodejs18
sudo yum clean metadata
sudo yum install -y nodejs git
node -v
git --version
```

## 3) Application deployment

### Create application user and directories
```bash
sudo useradd -r -m -s /bin/bash myapp || true
sudo mkdir -p /var/www/myapp /var/log/myapp
sudo chown -R myapp:myapp /var/www/myapp /var/log/myapp
```

### Clone repository
```bash
sudo mkdir -p /opt/src
sudo chown ec2-user:ec2-user /opt/src
cd /opt/src
git clone https://github.com/muntasirduet/LinuxServerWithCICD.git
cd /opt/src/LinuxServerWithCICD
```

### Build and publish
```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/MyApp.Api -c Release -o ./publish --no-build
```

### Deploy published files and permissions
```bash
sudo rsync -av --delete /opt/src/LinuxServerWithCICD/publish/ /var/www/myapp/
sudo chown -R myapp:myapp /var/www/myapp
sudo find /var/www/myapp -type d -exec chmod 755 {} \;
sudo find /var/www/myapp -type f -exec chmod 644 {} \;
```

## 4) Database setup

### PostgreSQL configuration
Edit `/var/lib/pgsql/data/pg_hba.conf` to keep local password auth:
```conf
host    all             all             127.0.0.1/32            md5
host    all             all             ::1/128                 md5
```
Restart PostgreSQL:
```bash
sudo systemctl restart postgresql
```

### Create database and user
```bash
sudo -u postgres psql <<'SQL'
CREATE USER myuser WITH PASSWORD 'REPLACE_STRONG_DB_PASSWORD';
CREATE DATABASE myappdb OWNER myuser;
GRANT ALL PRIVILEGES ON DATABASE myappdb TO myuser;
SQL
```

### Connection string configuration
Update `/var/www/myapp/appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=myappdb;Username=myuser;Password=REPLACE_STRONG_DB_PASSWORD"
  },
  "Jwt": {
    "Key": "REPLACE_WITH_STRONG_SECRET_KEY_MIN_32_CHARS_1234",
    "Issuer": "MyApp",
    "Audience": "MyAppUsers"
  },
  "AllowedOrigins": "https://yourdomain.com"
}
```

### Run EF Core migrations
This repository may not include migrations yet. If needed:
```bash
cd /opt/src/LinuxServerWithCICD
dotnet tool install --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add InitialCreate --project src/MyApp.Infrastructure --startup-project src/MyApp.Api
dotnet ef database update --project src/MyApp.Infrastructure --startup-project src/MyApp.Api
```

## 5) Nginx configuration

### Reverse proxy configuration
Copy template and edit domain:
```bash
sudo cp /opt/src/LinuxServerWithCICD/deploy/nginx/myapp.conf /etc/nginx/conf.d/myapp.conf
sudo nano /etc/nginx/conf.d/myapp.conf
```

Recommended `/etc/nginx/conf.d/myapp.conf`:
```nginx
server {
    listen 80;
    server_name yourdomain.com www.yourdomain.com;

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### SSL/TLS with Let's Encrypt
```bash
sudo amazon-linux-extras install epel -y
sudo yum install -y certbot python3-certbot-nginx
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com
sudo certbot renew --dry-run
```

### Nginx performance optimization
Add to `/etc/nginx/nginx.conf` under `http {}`:
```nginx
gzip on;
gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;
keepalive_timeout 65;
client_max_body_size 10m;
```
Validate and restart:
```bash
sudo nginx -t
sudo systemctl restart nginx
```

## 6) Systemd service configuration

Copy existing template:
```bash
sudo cp /opt/src/LinuxServerWithCICD/deploy/linux/myapp.service /etc/systemd/system/myapp.service
sudo systemctl daemon-reload
sudo systemctl enable myapp
sudo systemctl start myapp
```

Service management:
```bash
sudo systemctl status myapp
sudo systemctl restart myapp
sudo systemctl stop myapp
```

Log monitoring:
```bash
sudo journalctl -u myapp -f
sudo journalctl -u myapp --since "1 hour ago"
```

## 7) GitHub Actions CI/CD integration

Repository workflow: `.github/workflows/deploy.yml`

### Required GitHub Secrets
- `SERVER_IP` = EC2 public IP / DNS
- `SERVER_USER` = SSH user (usually `ec2-user`)
- `SSH_PRIVATE_KEY` = private key matching server `authorized_keys`

### SSH key management
Generate CI deploy key pair locally:
```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ./github-actions-deploy-key
```
- Add public key (`github-actions-deploy-key.pub`) to `/home/ec2-user/.ssh/authorized_keys` on EC2.
- Add private key content (`github-actions-deploy-key`) into GitHub secret `SSH_PRIVATE_KEY`.

### Automated deployment behavior
On push to `main`, workflow will:
1. `dotnet restore`
2. `dotnet build -c Release`
3. `dotnet test -c Release`
4. `dotnet publish src/MyApp.Api -c Release -o ./publish --no-build`
5. Copy publish output to `/var/www/myapp`
6. `sudo systemctl restart myapp`

### Test pipeline safely
- Open a PR to verify build/test stage.
- Merge to `main` to trigger deployment stage.
- Confirm with:
  ```bash
  sudo systemctl status myapp
  curl -I https://yourdomain.com/health
  ```

## 8) Monitoring and maintenance

### Application logs
```bash
sudo journalctl -u myapp -f
sudo journalctl -u myapp -n 200 --no-pager
```

### Health checks
App exposes:
- `GET /health`

Commands:
```bash
curl http://localhost:5000/health
curl -k https://yourdomain.com/health
```

### Auto-restart configuration
Already configured in `myapp.service`:
- `Restart=always`
- `RestartSec=10`

### Backup strategy
- Database backup (daily cron example):
  ```bash
  pg_dump -U myuser -h localhost myappdb | gzip > /var/backups/myappdb_$(date +%F).sql.gz
  ```
- Keep app configuration backups:
  - `/var/www/myapp/appsettings.Production.json`
  - `/etc/nginx/nginx.conf`
  - `/etc/nginx/conf.d/myapp.conf`
  - `/etc/systemd/system/myapp.service`

## 9) Troubleshooting

### Common errors and fixes
- **502 Bad Gateway (Nginx):**
  - Check app: `sudo systemctl status myapp`
  - Check app port: `sudo ss -ltnp | grep 5000`
- **App crash on startup:**
  - Validate config/JWT/connection string in `appsettings.Production.json`
  - Check logs: `sudo journalctl -u myapp -n 200 --no-pager`
- **Database connection failed:**
  - Confirm PostgreSQL running: `sudo systemctl status postgresql`
  - Test login: `psql -h localhost -U myuser -d myappdb`
- **SSL certificate issues:**
  - `sudo certbot certificates`
  - `sudo certbot renew --dry-run`

### Useful debug commands
```bash
dotnet --info
sudo systemctl status myapp nginx postgresql
sudo nginx -t
sudo tail -n 200 /var/log/nginx/error.log
```

### Log analysis quick checks
```bash
sudo journalctl -u myapp --since "30 min ago" | grep -E "error|fail|exception" -i
sudo grep -E "error|crit|warn" -i /var/log/nginx/error.log
```
