Coverline v0.3.1：连不上 GitHub 时自动更新也能用。

- 检查更新时先查 GitHub API（8 秒超时），失败就改读 jsDelivr 上的 `latest.json`（本仓库 `update-feed` 分支，CI 每次发布后自动更新；cdn / fastly / testingcf 三个域名依次尝试）。
- 下载更新包先走 github.com，失败就依次通过公共下载代理（默认 ghfast.top、gh-proxy.com、ghproxy.net、gh.llkk.cc）；每个来源单独超时。
- SHA-256 只采信 GitHub 或 jsDelivr 的 latest.json；从代理下到的文件哈希不符（包括错误网页）会被丢弃并换下一个来源。update.log 记录实际使用的来源。
- settings.json 新增可选 `"updateMirrors": [...]`，可替换默认代理列表（`[]` = 只用 github.com）。
- GitHub 限流时不再整次跳过，直接改用备用源。

注意：公共代理由第三方维护，可能失效；jsDelivr 有缓存，新版本可能晚几个小时出现在备用源。**v0.3.0 需要能连上 GitHub 一次才能升到 v0.3.1**，之后才有备用源；更早版本请手动下载。

New in v0.3.1: auto update works when GitHub is unreachable. If the GitHub API fails (8 s timeout) Coverline reads `latest.json` from the repo's `update-feed` branch via jsDelivr; the zip is downloaded from github.com first and then through public proxies (ghfast.top, gh-proxy.com, ghproxy.net, gh.llkk.cc), each with its own timeout. The SHA-256 only ever comes from GitHub or the jsDelivr feed, and any copy that doesn't match is discarded. Optional `updateMirrors` in settings.json overrides the proxy list. Public proxies may disappear and jsDelivr may lag a release by a few hours. v0.3.0 needs to reach GitHub once to get 0.3.1.

需要 Windows 10 1809 或更高版本。程序未签名，首次运行可能出现 SmartScreen 提示。灵感来自 Mac 应用 Sleeve（Replay Software），本项目为独立实现，与其无关联。
