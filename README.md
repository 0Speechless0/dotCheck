# dotCheck

Blazor Web App + MongoDB Community 的項目認結 / 結清系統範例實作。

## 技術

- .NET 10 / Blazor Web App
- Interactive Server
- MongoDB Community + official `MongoDB.Driver`
- ASP.NET Core Cookie Authentication
- `PasswordHasher<TUser>` password hashing
- Linux filesystem for invoice images
- Apache reverse proxy / port 8181

## 目前包含

1. 項目表單新增
2. 項目認結
3. 已認結 / 已結清查詢與統計
4. 管理者項目結清
5. 使用者註冊與登入
6. 未認結理由
7. 規章辦法 Menu modal
8. RWD
9. 2MB invoice image limit
10. MongoDB indexes
11. Blazor `Virtualize` lazy query

## 注意

本環境沒有安裝 .NET SDK，因此這份產物是在無 SDK 編譯器的環境下依 .NET 10 API 與專案結構產生，尚未在此容器中執行 `dotnet build`。在你的 VS Code / Linux 26 主機上首次 restore/build 時應立即檢查任何 SDK 或套件差異。

## 本機開發

```bash
dotnet --version
dotnet restore
dotnet build
dotnet run
```

瀏覽：`http://localhost:5000`

MongoDB 預設：`mongodb://127.0.0.1:27017`，Database：`dotCheck`。

## 建立管理員

建議用環境變數，不要把管理者密碼提交到 Git：

```bash
export Admin__UserName=admin
export Admin__Password='change-me-now'
dotnet run
```

第一次啟動會建立 Admin；建立完成後可移除環境變數。

## 發票儲存

預設使用執行程序帳號的 Home：

```text
~/dotCheck/YYYY/MM/<itemId>.jpg
```

也可以設定：

```text
FileStorage__RootPath=/home/dotcheck/dotCheck
```

MongoDB 只保存相對路徑，不保存實體檔案。

## Apache / Linux 26

將 `deploy/apache-dotcheck.conf` 放入 Apache 設定目錄，例如：

```bash
sudo cp deploy/apache-dotcheck.conf /etc/apache2/sites-available/dotcheck.conf
sudo a2enmod proxy proxy_http proxy_wstunnel headers
sudo a2ensite dotcheck.conf
sudo apachectl configtest
sudo systemctl reload apache2
```

確認 Apache 有 `Listen 8181`。

Kestrel 只監聽：

```text
127.0.0.1:5000
```

外部透過：

```text
http://<server-ip>:8181
```

## systemd

`deploy/dotcheck.service` 假設：

- 部署到 `/opt/dotCheck`
- 使用 `dotcheck` Linux 帳號
- 發票路徑 `/home/dotcheck/dotCheck`
- MongoDB 為本機 27017

發布：

```bash
dotnet publish -c Release -o ./publish
sudo mkdir -p /opt/dotCheck
sudo cp -r ./publish/* /opt/dotCheck/
sudo cp deploy/dotcheck.service /etc/systemd/system/dotcheck.service
sudo systemctl daemon-reload
sudo systemctl enable --now dotcheck
sudo systemctl status dotcheck
```

## 認結規則

對每一個 Pending 項目：

```text
approvedUserCount * 2 > activeUserCount
```

成立時更新：

```text
Pending -> Confirmed
```

剛好一半不算通過。

同一個使用者對同一項目使用 `$addToSet`，因此不會因重複提交而重複計數。

## 修改項目規則

項目申請人可以在「我的未認結理由」修改項目名稱、金額、發票。

修改後：

```text
ApprovedUserIds 清空
Pending
既有未認結理由刪除
重新進入認結流程
```

已結清項目不可修改。

## 安全性

- 密碼只儲存 hash
- Authentication Cookie `HttpOnly`
- `/admin/settlement` 使用 Role-based Authorization
- 發票檔案不直接暴露實體路徑
- `/invoice/{id}` 需要登入
- Server 與檔案路徑皆再次檢查
- 上傳大小與 MIME 類型由後端再次驗證

正式環境還應搭配 HTTPS、MongoDB authentication、秘密管理、備份、日誌與權限最小化。


## 依賴版本（2026-09-19 查核）

- `MongoDB.Driver` 3.11.2：MongoDB 官方 C# Driver，目前 NuGet 頁面列出的版本；此版本發布於 2026-09-10。
- `Microsoft.Extensions.Identity.Core` 10.0.12：用於 `PasswordHasher<TUser>`，NuGet 頁面列出的 10.0.12 發布於 2026-09-08。

實際安裝時請讓 NuGet restore 完成，並將 `obj/project.assets.json` 產生後再提交 lockfile 或更新依賴。
