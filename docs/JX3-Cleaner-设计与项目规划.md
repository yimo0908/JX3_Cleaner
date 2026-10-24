# JX3-Cleaner 设计与项目规划

> 一款面向《剑网3》V4 客户端的冗余数据清理工具，把「手动清理」文档里的人肉操作，变成可勾选、可预览、可回滚的图形化流程。

| 项目 | 内容 |
|---|---|
| 文档版本 | **v0.12（修复框架依赖发布 + 警告清零）** |
| 参考依据 | JX3BOX《剑网3客户端（V4版本）瘦身手册【非官方！】》第 2 节「手动清理」 |
| 结构依据 | 2026-10-24 实机客户端结构核对（只记录目录形态，**不含任何本机路径，不写入项目默认值**） |
| 目标平台 | Windows 10 1809+ / Windows 11，x64 |
| 交付形态 | 单目录绿色版（无需安装，可选单文件） |

## 修订记录

| 版本 | 日期 | 变更 |
|---|---|---|
| v0.1 | 2026-10-24 | 初版设计基线 |
| v0.2 | 2026-10-24 | 按实测结构修正路径与命名；新增手动定位与目录校验、幻境云图、图片预览页、云团监分组、旧团监清删、直接删除红色样式；错误账号改为默认勾选 |
| v0.3 | 2026-10-24 | 实机确认 `DCIM` 路径正确（关闭原 R-03）；截图/云图页支持 mp4 视频，不内联播放，双击调系统默认播放器；缩略图改用 Shell `IShellItemImageFactory` 以覆盖视频首帧 |
| v0.4 | 2026-10-24 | 落地 .NET 10 + WPF 实现；发现 `combat_logs`（`.jcl` 战斗日志）实测占 18.66 GB，新增为数据分类；配置改为程序目录内便携存储；启动强制软件渲染 |
| v0.5 | 2026-10-24 | 茗伊类分类名加「（茗伊插件）」后缀；战斗日志更名 JCL战斗日志；新增金团记录（`gkp`）；角色数据改为账号文件夹整体、行后括号角色名、不展开；茗伊各类不再显示括号角色名与 global_id；云团监增加灰色括号时间；体积列放弃对齐 |
| v0.6 | 2026-10-24 | 三组划分（茗伊插件 / 懒人插件 / 游戏数据，左右两栏同步），取消「（茗伊插件）」后缀；分类行显示可点击的相对路径；顶级分类可直接勾选；旧版团监改为存在才勾选；角色数据有过期区服时可展开逐个大区勾选；「开始清理」常驻；图片浏览页落地 |
| v0.7 | 2026-10-24 | 截图 / 幻境云图从数据清理页移除（扫描跳过），改由图片页勾选清理，复用同一执行链路；图片页按月分组 + 左侧时间导航；缩略图改行级虚拟化（实测 485 文件只实体化 55 张卡片）；底栏下放到各页 |
| v0.8 | 2026-10-24 | 新增设置页（七项各一行 + 加减框 + 保存后关闭）；首次启动强制先过设置页；五项保留策略改为用户可调；时间筛选显示「已过滤 N 项」；全部窗口与控件适配 Win11 圆角；补 101 条单元测试 |
| v0.12 | 2026-10-25 | 修复 `-p:SelfContained=false` 发布报 NETSDK1176：单文件压缩改为随 `$(SelfContained)` 条件开启；Core 声明 `SupportedOSPlatform(windows)`、App/Diag/Tests 统一 `net10.0-windows`，`CA1416` 与 `CS8604` 全部清零；Release 不产 pdb；开源整理：仓库内账号 / 角色名 / 路径示例统一化名，截图中的本机路径与角色名打码；新增应用图标（唐门面具素材，`Assets/JX3Cleaner.ico` 7 档尺寸），`Window.Icon` 与 exe 图标同源；许可证定为 MIT；`dist\` 同时存放自包含与框架依赖两个单文件 |
| v0.11 | 2026-10-25 | 树行不再有「选中」语义：`TreeViewItem` 改为自写模板（不引用 `IsSelected`），点组标题或任意行不再出现蓝色背景 |
| v0.10 | 2026-10-25 | 懒人插件 / 懒人插件数据 / 旧版团监数据 三类标题后补黄色「（已弃用）」；目录不存在的分类在左右两栏一律隐藏（左栏不再灰显禁用，组内全空时连组标题与分隔线一起隐藏）；测试增至 135 条 |
| v0.9 | 2026-10-25 | 新增运行环境自检（R-10）：启动最早期判系统/位数/运行时，错误弹窗拦截、提示只记录；发布形态用程序集元数据判准；新增自包含单文件发布配置（61.7 MB，目标机免装 .NET）；`RollForward=Major`；设置页增加只读「运行环境」行；测试增至 127 条 |

### v0.8 变更清单

| # | 变更 | 影响章节 |
|---|---|---|
| 1 | `SettingsWindow`：客户端地址（真实地址 + 扫盘 + 手动定位）、默认清理方式、N/X/Y/Z/M 五项保留策略 | §6.6 |
| 2 | `NumberStepper` 加减框：不可键入，条数步长 1、天数步长 5 | §6.6 |
| 3 | 首次启动 / 配置被删 / 目录失效 → 先过设置页，校验成功并保存后才进主界面 | §6.6 §8.2 |
| 4 | `KeepRule` / `KeepSettings` / `RetentionPolicy`：保留策略从写死改为设置驱动，纯函数可测 | §5.9 §4.2 |
| 5 | `ScanResult.Filtered`：时间窗口排除的条数与体积，显示在下拉框正下方 | §6.2 |
| 6 | Win11 圆角：`Themes/Win11.xaml` 控件模板 + DWM `DWMWA_WINDOW_CORNER_PREFERENCE` | §6.7 |
| 7 | 明确不做：日志查看页、账号名手动校正、干跑清单页、图片页体积排序 / 合并浏览 / 键盘操作、多显示器适配 | §1.3 |
| 8 | 单元测试 101 条（保留策略、账号解析、云团监分组、计划构建、路径守卫、金团正则、媒体命名、目录校验、扫描端到端） | §11 |

### v0.7 变更清单

| # | 变更 | 影响章节 |
|---|---|---|
| 1 | `CategoryDefinition.MediaOnly`：截图 / 幻境云图扫描时跳过，数据页两栏都不再出现 | §3.1 §6.2 |
| 2 | 图片页支持勾选 + 清理方式 + 清理按钮，选中项转 `ScanNode` 交回主窗口 | §6.4 |
| 3 | 按月分组（文件名拍摄时间优先）+ 左侧 `YYYY-MM` 导航，点击定位 | §6.4 |
| 4 | 缩略图行级虚拟化 + 按需解码 + 滚动释放 | §6.4 §9.5 |
| 5 | 窗口级底栏下放到数据页，图片页有独立底栏 | §6.2 §6.4 |

### v0.6 变更清单

| # | 变更 | 影响章节 |
|---|---|---|
| 1 | 分类行标题后显示灰色相对路径，点击在资源管理器打开（`{gid}` 占位时打开 `my#data`）；展开的细项不显示 | §6.2 |
| 2 | 旧版团监改为「存在才显示并默认勾选」 | §5.6 |
| 3 | 角色数据存在过期区服时可展开，只列出过期大区供单独勾选 | §5.4 §5.4b |
| 4 | 「开始清理」常驻，未勾选时点击给出提示而非置灰消失 | §6.2 |
| 5 | 顶级分类可勾选（三态：全选/部分/未选），勾选即级联全部分类内条目 | §5.8 |
| 6 | 左右两栏按 茗伊插件 / 懒人插件 / 游戏数据 三组划分，取消分类名里的「（茗伊插件）」 | §3.2 §6.2 |
| 7 | 图片浏览页落地：Shell 缩略图、mp4 首帧、实时预览、双击调系统默认程序 | §6.4 §9.5 |

### v0.2 变更清单

| # | 变更 | 影响章节 |
|---|---|---|
| 0 | 以本机实测结构校准全部路径（`@zhcn_hd` 后缀、`my#data` 实际大小写、`team_mon/remote` 真实文件形态） | §2 §3 |
| 1 | 新增「手动定位客户端目录」按钮与目录结构校验（`bin\zhcn_hd` + `bin64` + 清理所需目录） | §4.4 |
| 2 | 懒人插件改为**条件默认勾选**（仅当 `LR_Plugin` 或 `LR_Plugin@DATA` 存在） | §3.2 §5.2 |
| 3 | 错误账号改为**默认勾选** | §5.3 |
| 4 | 新增数据类型「幻境云图」`DCIM\` | §3.2 |
| 5 | 截图与幻境云图独立成页，实时缩略图 + 右键菜单 | §6.4 |
| 6 | 「团监数据」更名「云团监数据」，路径改为 `team_mon\remote\`，按**组**选择 + 勾选前备份提示 | §3.2 §5.5 |
| 7 | 旧路径 `userdata\TeamMon\` 存在时直接整目录清理 | §5.6 |
| 8 | `cmd.exe del` 外显为「直接删除」，红色底 | §6.5 |

### v0.3 变更清单

| # | 变更 | 影响章节 |
|---|---|---|
| 1 | 实机确认 `DCIM` 即幻境云图真实路径，原「路径待确认」风险关闭 | §2.1 §12 |
| 2 | `ScreenShot` 与 `DCIM` 均可能包含 mp4（游戏内置录制）；页面不内联播放，双击调本地默认播放器 | §6.4 |
| 3 | 缩略图方案由 `BitmapImage` 改为 Shell `IShellItemImageFactory`，统一覆盖图片与视频首帧 | §9.5 |

### v0.5 变更清单

| # | 变更 | 影响章节 |
|---|---|---|
| 1 | 体积列不再追求右对齐，保持现状（TreeViewItem 默认模板限制，收益不抵风险） | §6.2 §14 |
| 2 | 状态栏文案由 `已校验 · 路径` 改为只显示 `已校验` | §6.2 |
| 3 | 聊天记录 / 战斗记录 / JCL战斗日志 / 云团监数据 名称后加「（茗伊插件）」 | §3.2 |
| 4 | 「战斗日志」更名「JCL战斗日志」 | §3.2 |
| 5 | 新增「金团记录（茗伊插件）」：`...\userdata\gkp`，仅清 `年-月-日-时-分-秒_地图名.gkp.jx3dat`，`current.gkp.jx3dat` 不动 | §3.2 §5.5b |
| 6 | 角色数据改为**以账号文件夹为整体**，行后灰色括号显示该账号全部角色名，不再展开大区/服务器/角色 | §5.3 §6.2 |
| 7 | 茗伊各类数据行不再显示括号角色名与 `global_id` | §6.2 |
| 8 | 云团监数据增加灰色括号时间（分组名的哈希难以辨认） | §5.5 |

---

## 1. 项目背景与目标

### 1.1 背景

官方客户端「资源管理 → 清理」只能回收游戏自身产生的更新包、渲染缓存。真正的大头在 `bin\zhcn_hd` 下：本机实测 `interface\my#data` 单目录就有 **30,548 个文件 / 21.0 GB**，其中仅单个账号的 `chat_log` 就占 652 MB。这些目录没有任何图形入口，用户要在资源管理器里逐层翻找，自己判断哪个 `global_id@zhcn_hd` 对应哪个账号、哪个大区是合并前的残留。

风险集中在两处：**认错账号**、**删掉还需要的角色配置**。

### 1.2 目标

1. 自动探测或手动定位客户端，**校验目录结构**后才允许扫描。
2. 三/四维交叉圈定清理范围：数据类型 × 涉及账号（角色）× 文件时间 × 保留策略。
3. 把 `global_id` 翻译成**可识别的游戏账号名**。
4. 对异常结构（无区服账号、过期大区、旧版残留目录）主动告警。
5. 提供「移动到回收站」与「直接删除」两种方式，前者可撤销。
6. 截图 / 幻境云图提供可视化浏览，看图再决定删不删。
7. 每次清理产出可审计清单。

### 1.3 非目标

- 不做客户端完整性校验 / 修复（启动器职责）。
- 不清理 `*.pak` 等游戏本体资源。
- 不做云同步、多机管理。

**v0.8 明确不做**（曾经列入待办，用户已决定不再排期）：

- 审计日志的查看页与日志目录入口（清理仍写 `logs\clean-*.json`，只是不提供浏览界面）。
- 账号名手动校正 UI（自动解析 + `未知账号…后四位` 兜底）。
- 干跑清单独立预览页（确认弹窗里已给出条数、总体积与被拦截数）。
- 图片页按体积排序、截图与云图合并浏览、键盘操作（Ctrl+A / 方向键 / Delete）。
- 多显示器与高 DPI 的列数换算专项适配。
- 不替代官方「资源管理 → 清理」。

---

## 2. 实机结构核对（2026-10-24）

> 本章只描述目录形态（相对 `bin\zhcn_hd`）。**项目中不得出现任何本机盘符硬编码**，配置文件与默认值必须为空或由用户选择；仓库内的账号、角色名与路径示例均为化名。

### 2.1 顶层

| 目录 | 与清理相关 | 实测 |
|---|---|---|
| `bin\zhcn_hd\bin64` | 目录校验锚点 | ✅ 存在 |
| `bin\zhcn_hd\DCIM` | 幻境云图 | ✅ **路径已实机确认**，当前 0 文件；可含 mp4 录制 |
| `bin\zhcn_hd\ScreenShot` | 截图 | 65 文件 / 105.9 MB；可含 mp4 录制 |
| `bin\zhcn_hd\interface` | 插件与茗伊数据 | 21.0 GB |
| `bin\zhcn_hd\userdata` | 角色数据 | 7,805 文件 / 21.5 MB |
| `bin\zhcn_hd\homelanddir` | 蓝图（`Blueprints`、`FurnitureList`） | 0.2 MB |
| `bin\zhcn_hd\hairdyeingdata` | 染发 | 1 文件 |
| `bin\zhcn_hd\selfiedata` | 幻境云图相关（`AiBodyMotion`） | 0 文件，暂不纳入 |

### 2.2 `interface\my#data`

**修正**：文章写的是 `MY#DATA`，实际目录名为 `my#data`（Windows 不区分大小写，代码里必须用 OrdinalIgnoreCase 比较，日志保留实际大小写）。

目录构成（共 44 个 `*@zhcn*` 条目）：

| 类型 | 形态 | 数量 | 处理 |
|---|---|---|---|
| 账号目录 | `{gid}@zhcn_hd` | 40 | 参与账号映射 |
| 账号目录（旧后缀） | `{gid}@zhcn` | 1 | 同样处理，正则需兼容两种后缀 |
| 公共目录 | `!all-users@zhcn_hd` | 1 | 云团监 / 公共数据，不参与账号映射 |
| 区服目录 | `#电信区_示例服@zhcn_hd` 等 | 3 | 区服级公共数据，不参与账号映射 |
| 缓存目录 | `#cache` | 1 | 不参与 |

**账号目录内部结构**（实测 `{gid}@zhcn_hd`）：

```
audio/  cache/  config/  data/  export/  font/  logs/  temporary/  userdata/
info.jx3dat  manifest.jx3dat
角色甲/            ← 玩家名空目录，0 内容
```

✅ **R-01 账号名映射规则得到验证**：排除固定目录后，空目录名即玩家名。实测样本（`global_id` 与角色名均已化名）：

| global_id | 玩家名 |
|---|---|
| `1000000000000000001@zhcn_hd` | 角色甲 |
| `1000000000000000002@zhcn_hd` | 角色乙 |
| `1000000000000000003@zhcn_hd` | 角色丙、角色丁（同账号两个角色） |
| `1000000000000000004@zhcn_hd` | 角色戊（另有同名 gid 空目录，需过滤纯数字） |
| `1000000000000000005@zhcn`（旧后缀） | 角色己 |

⚠️ 重要：`audio` `cache` `export` `font` `logs` 这些**固定目录本身也常常是空的**，所以「空目录 = 玩家名」必须配合固定目录白名单，否则会把 `audio` 当成角色名。

### 2.2.1 战斗日志（原文未列出，实测为全盘最大项）

| 项 | 实测 |
|---|---|
| 路径 | `interface\my#data\{gid}@zhcn_hd\userdata\combat_logs` |
| 文件形态 | `2024-04-12-19-44-16-英雄鹿桥驿(639)-骨面天君(120857).jcl` |
| 体量 | 单账号 8.8 GB / 8,220 个文件；全盘合计 **18.66 GB / 20,277 个文件** |
| 说明 | 原文写于 2021 年，当时该目录还不是大头。删除不影响游戏运行，但历史战斗回放会丢失，因此**默认不勾选**，交由用户决定。 |

### 2.3 `userdata` 层级

```
userdata/
  config.dat  custom.dat  custom.dat.addon  customEnter.dat  customconfigure.dat
  historyaccount.dat  recent_server.dat  rolenum.dat      ← 根级文件，非账号
  fight_stat/                                            ← 目录但不是账号！
  {账号}/
    config.dat  custom.dat  login.ini  recent_server.dat  rolenum.dat
    {大区}/{服务器}/{角色}/
```

实测：

- 大区名仅出现 `电信区`(29 次) 和 `双线区`(3 次)，无 `无界区`，本机**没有**过期大区名（规则仍需保留）。
- 错误账号实例：`user@qq.com` 只有 3 个 `.dat`，**无任何大区目录** → 命中 R-02。
- 账号名形态多样：`13800000000`、`user@qq.com`、`user_01`、`U123456789` —— 不能用「纯数字」判定账号。

### 2.4 云团监

```
interface\my#data\!all-users@zhcn_hd\userdata\
  team_mon\  audio\  local.jx3dat  metalist.jx3dat
  team_mon\remote\  r-xxxx.jx3dat + r-xxxx.meta.jx3dat
  TeamMon\   Data\  Meta\  MetaList.jx3dat      ← 旧版遗留，仍存在
```

| 项 | 实测 |
|---|---|
| `team_mon\remote` | 150 文件 = **75 组**（严格成对） |
| 目录体积 | 73.5 MB |
| `local.jx3dat` | 存在（本地修改数据，**永不删除**） |
| 旧 `TeamMon\` | ✅ 存在，含 `Data/`、`Meta/`、`MetaList.jx3dat` |

### 2.5 懒人插件

**实测不存在**：`interface` 下无 `LR_Plugin` / `LR_Plugin@DATA`。

⚠️ 陷阱：`interface\LM`（15 个 `LM_*` 子目录）与 `interface\lm#data` 看起来很像懒人插件，但 `LM_!Base\info.ini` 里写着 `name=枫影基础库`、`package=LM` —— 这是**枫影插件**，与懒人插件无关，绝不能纳入清理。

---

## 3. 可清理项清单

路径基准：`<客户端根>\bin\zhcn_hd\`。

### 3.1 清单

| 编号 | 分类 | 相对路径 | 粒度 | 实测体积 | 默认勾选 | 风险 |
|---|---|---|---|---|---|---|
| C-01 | 聊天记录 | `interface\my#data\{gid}@zhcn(_hd)\userdata\chat_log` | 文件 | 单账号最大 652 MB | 是（保留最近 2 个） | 中 |
| C-02 | 战斗记录 | `...\{gid}@zhcn(_hd)\userdata\fight_stat` | 文件 | 8.2 MB | 否 | 低 |
| C-02b | **JCL战斗日志**（原文未列） | `...\{gid}@zhcn(_hd)\userdata\combat_logs` | 文件 | **18.66 GB** | 否 | 中 |
| C-02c | **金团记录**（v0.5 新增） | `...\{gid}@zhcn(_hd)\userdata\gkp` | 文件（按账号聚合） | 809 文件 / 4.6 MB | 否 | 低 |
| C-03 | 懒人插件（已弃用） | `interface\LR_Plugin` | 目录 | 未检出 | **存在才显示并勾选** | 低 |
| C-04 | 懒人插件数据（已弃用） | `interface\LR_Plugin@DATA` | 目录 | 未检出 | **存在才显示并勾选** | 低 |
| C-05 | **云团监数据** | `interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote` | **组** | 75 组 / 73.5 MB | 否（勾选前弹窗） | 低 |
| C-06 | **旧版团监数据**（已弃用） | `interface\my#data\!all-users@zhcn_hd\userdata\TeamMon` | 整目录 | 2.5 MB | **存在才显示并勾选** | 低 |
| C-07 | 家园蓝图 | `homelanddir\Blueprints` | 文件 | 0.2 MB | 否 | 高 |
| C-08 | 截图（图片 + mp4） | `ScreenShot` | 文件 | 106.9 MB | 否 | 高 |
| C-09 | **幻境云图**（图片 + mp4） | `DCIM` | 文件 | 659.3 KB | 否 | 高 |
| C-10 | 角色数据 | `userdata\{账号}`（**以账号文件夹为整体**；有过期区服时可展开单独勾选） | 账号 | 11.3 MB | 否（错误账号除外） | 高 |
| C-11 | 染发方案 | `hairdyeingdata` | 文件 | 1 文件 | 否 | 中 |
| C-12 | 设计站图片 | `sharestationdata` | 文件 | 未检出 | 是 | 低 |

> **分组（v0.6）**：界面左右两栏都按三组划分 ——
> **茗伊插件**（C-01 / C-02 / C-02b / C-02c / C-05 / C-06）、
> **懒人插件**（C-03 / C-04）、
> **游戏数据**（C-07 / C-10 ~ C-12；C-08、C-09 已划到图片页）。
> 分类名里不再带「（茗伊插件）」后缀，组标题就是归属说明。

> **C-08 / C-09 归「截图与云图」页（v0.7）**：这两类不再出现在「数据清理」页，
> 扫描时直接跳过（`CategoryDefinition.MediaOnly = true`），不再为上千个媒体文件建树节点。
> 清理改在图片页勾选完成，但仍走同一套 `CleanPlanBuilder` + `PathGuard` + 审计日志。

> **C-08 / C-09 媒体类型**：两个目录都可能出现游戏内置录制的视频，格式为 `.mp4`。清理界面**不做任何内联播放**——视频只显示首帧缩略图与播放角标，双击交给系统默认播放器。可识别扩展名：
> 图片 `.jpg .jpeg .png .bmp .gif`，视频 `.mp4`。其它扩展名不参与预览，但仍计入体积并可选清理。

> **C-02c 命名规则**：金团记录文件名形如 `2021-08-07-21-45-33_25人普通白帝江关.gkp.jx3dat`。
> 每个账号的 `gkp` 目录下还有一个 `current.gkp.jx3dat`（当前正在进行的记录），
> 它不匹配 `^\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}_`，因此**永远不在清理范围、也不计入体积**。

### 3.2 与 v0.1 的差异

| 项 | v0.1 | v0.2 |
|---|---|---|
| 团监 | `TeamMon\Data` 按文件选 | 更名「云团监数据」，改 `team_mon\remote`，**按组**选 |
| 旧 TeamMon | 未涉及 | 新增 C-06，存在即整目录清理，默认勾选 |
| 懒人插件 | 默认勾选 | 条件默认勾选（存在才勾） |
| 幻境云图 | 无 | 新增 C-09 |
| 错误账号 | 默认不勾选 | **默认勾选** |

---

## 4. 需求规格

### 4.1 功能需求

| 编号 | 需求 | 优先级 |
|---|---|---|
| FR-01 | 自动探测客户端目录（注册表 + 常见路径） | P0 |
| FR-02 | **手动定位按钮 + 目录结构校验**（见 4.4） | P0 |
| FR-03 | 扫描并聚合体积/文件数/最近修改时间，可取消 | P0 |
| FR-04 | 清理范围四维筛选：数据类型、涉及账号、文件时间、保留策略 | P0 |
| FR-05 | 茗伊数据按账号展示，显示游戏账号名称；**不显示括号角色名与 global_id** | P0 |
| FR-06 | ~~角色数据按四级树展示~~ → **v0.5 起改为以账号文件夹为整体，不展开**（防止误删单个角色的按键/界面/宏） | P0 |
| FR-07 | **仅角色数据**在账号行后用 `（角色1、角色2…）` 小号灰字显示角色名，超 3 个折叠 `+n` | P0 |
| FR-08 | 账号下无区服文件 → 错误账号，标红，**默认勾选** | P0 |
| FR-09 | 过期大区（非 电信区/双线区/无界区）→ 角标 + 灰色列出区名 + 勾选时确认弹窗 | P0 |
| FR-10 | 清理方式：移动到回收站 / **直接删除**（红色） | P0 |
| FR-11 | 干跑预览 + 审计日志 | P0 |
| FR-12 | **截图与幻境云图独立页面**：缩略图预览 + 右键菜单 + **勾选清理** + 月份导航 | P0 |
| FR-13 | 云团监按组选择 + 勾选前备份提示 + 灰色括号时间 | P0 |
| FR-14 | 旧 TeamMon 目录存在时整目录清理 | P0 |
| FR-15 | 懒人插件存在性判定与条件默认勾选 | P0 |
| FR-16 | ~~账号名手动校正并本地持久化~~ → **v0.8 明确不做**，自动解析失败时显示 `未知账号…后四位` | — |
| FR-17 | 按体积排序、展开/折叠全部 | P2 |
| FR-18 | 中文路径与超长路径兼容 | P0 |
| FR-19 | 金团记录：仅清日期命名的 `.gkp.jx3dat`，`current.gkp.jx3dat` 不动 | P0 |
| FR-20 | 体积列无需右对齐（v0.5 明确放弃） | — |
| FR-21 | **设置页**：客户端地址（真实地址 / 扫盘 / 手动定位）、默认清理方式、五项保留策略，七个设置项各一行 | P0 |
| FR-22 | 五项保留策略可调：聊天记录 N 个、战斗记录 X 天、JCL 日志 Y 天、金团记录 Z 天、云团监 M 组；加减框不可键入，条数步长 1、天数步长 5 | P0 |
| FR-23 | 首次启动 / 配置被删 → 先过设置页，定位校验成功并保存后才进主界面 | P0 |
| FR-24 | 文件时间筛选后，在下拉框正下方提示「已过滤 N 项，约 XX MB」 | P0 |
| FR-25 | 窗口与全部控件适配 Win11 圆角 | P1 |

### 4.2 筛选模型

```csharp
// v0.8：保留策略从写死改为设置驱动，界面与扫描引擎共用同一份规则
public sealed class KeepSettings
{
    public int ChatLogCount   { get; init; } = 2;    // N：聊天记录保留最近几个
    public int FightStatDays  { get; init; } = 30;   // X：战斗记录保留最近几天
    public int CombatLogDays  { get; init; } = 30;   // Y：JCL 战斗日志保留最近几天
    public int GkpDays        { get; init; } = 90;   // Z：金团记录保留最近几天
    public int TeamMonGroups  { get; init; } = 5;    // M：云团监保留最近几组

    public KeepRule RuleFor(CleanCategory id);       // 其余分类 → KeepRule.None
}
```

- **数据类型** — 三组 14 类，见 §3.1；截图与幻境云图由图片页处理。
- **文件时间** — `LastWriteTime`，7/30/90/180/365 天与全部；目录取子树 `Max`。
  语义是「只清理早于该时间的条目」，因此**较新的条目会被排除**，排除量显示在下拉框下方。
- **保留策略** — 与时间条件取**并集保护**：即使时间命中，被保留的项也永不勾选。
- 判定全部收敛到纯函数 `RetentionPolicy.Split`：返回「保留前缀长度 + 可清理下标 + 被过滤下标」，
  三者在剩余部分里可能交错，所以必须返回下标而不是条数（v0.7 曾因此取错文件，已由单测锁死）。

### 4.3 非功能需求

| 编号 | 需求 |
|---|---|
| NFR-01 | 扫描 3 万文件 / 21 GB ≤ 3 s（SSD 热缓存），冷启动 ≤ 8 s |
| NFR-02 | 媒体页 500 项（图片 + mp4 混合）首屏 ≤ 1 s，滚动不掉帧，内存峰值 ≤ 300 MB |
| NFR-03 | UI 线程零磁盘 IO，全异步 + 可取消 |
| NFR-04 | 全程离线，不上传路径与账号信息 |
| NFR-05 | 单文件绿色版，无运行时安装依赖 |

### 4.4 客户端目录定位与校验（FR-02）

**交互**：路径框 + `浏览…` 按钮 + `校验` 状态徽标（绿勾 / 黄叹号 / 红叉）。

**接受两种输入并归一化**：

```csharp
static ClientRoot? Normalize(string picked)
{
    var p = Path.GetFullPath(picked).TrimEnd('\\');

    // ① 用户直接选了 zhcn_hd
    if (Path.GetFileName(p).Equals("zhcn_hd", StringComparison.OrdinalIgnoreCase)
        && Directory.Exists(Path.Combine(p, "bin64")))
        return new ClientRoot(
            Root: Path.GetDirectoryName(Path.GetDirectoryName(p))!, // …\JX3
            BinDir: Path.GetDirectoryName(p)!,
            ZhcnDir: p);

    // ② 用户选了客户端根（JX3）
    var zhcn = Path.Combine(p, "bin", "zhcn_hd");
    if (Directory.Exists(zhcn))
        return new ClientRoot(Root: p, BinDir: Path.Combine(p, "bin"), ZhcnDir: zhcn);

    return null;
}
```

**校验规则**：

| 级别 | 条件 | 表现 |
|---|---|---|
| 🔴 硬性 | 未匹配到 `bin\zhcn_hd` 结构 | 阻断，「所选目录下未找到 `bin\zhcn_hd`，请选择剑网3客户端根目录」 |
| 🔴 硬性 | `zhcn_hd\bin64` 不存在 | 阻断，「缺少 `bin64` 目录，可能不是 V4 客户端或版本过旧」 |
| 🟡 软性 | 某个清理目录缺失 | 放行，但该分类在树中隐藏，筛选栏置灰并注明「未检测到该目录」 |
| 🟢 通过 | 硬性全过 | 显示「已校验」，写入本地设置 |

**清理所需目录清单**（软性逐项校验）：

```
interface\my#data           → 茗伊、云团监、旧团监
interface\LR_Plugin         → 懒人插件
interface\LR_Plugin@DATA    → 懒人插件数据
userdata                    → 角色数据
ScreenShot                  → 截图
DCIM                        → 幻境云图
homelanddir\Blueprints      → 家园蓝图
hairdyeingdata              → 染发方案
sharestationdata            → 设计站
```

**持久化**：写入 `%LOCALAPPDATA%\JX3Cleaner\settings.json`，**不写进项目目录**，不预置任何机器路径。

```json
{
  "clientRoot": "…",
  "zhcnDir": "…",
  "verifiedAt": "2026-10-24T20:05:00+08:00",
  "schema": 2
}
```

启动时若缓存路径仍通过硬性校验则直接使用，否则回到「未定位」状态并要求用户手动选择。

---

## 5. 规则引擎

### 5.1 R-01 账号名解析

输入：`interface\my#data\{gid}@zhcn_hd\` 或 `{gid}@zhcn\`

```
1. 目录名匹配 ^(\d{6,})@zhcn(_hd)?$  （忽略大小写）
   → 不匹配的：!all-users@…、#cache、#区服@… 一律排除，归入"公共/区服数据"
2. 扫描该目录下一层子目录：
   - 名字在固定白名单内 → 跳过
     白名单 = audio, cache, config, data, export, font, logs,
              temporary, userdata, img, lang, licenses, src
   - 名字为纯数字 → 跳过（实测存在与 gid 同名的空目录）
   - 长度 1–16、非空、不含 \ / : * ? " < > | → 收集为角色名候选
3. 候选拼接：1 个直接用；多个用「、」连接；>3 个显示前 3 + "+n"
4. 无候选 → "未知账号" + gid 尾 4 位
5. 结果缓存 account-map.json（键 gid，值 display/source/dirMtimeUtc）
```

实测覆盖率：40 个账号中，除个别只有 gid 同名空目录的以外全部可解析。

### 5.2 R-02 懒人插件条件勾选

- 扫描时判定 `DirExists(interface\LR_Plugin) || DirExists(interface\LR_Plugin@DATA)`
- 存在 → `DefaultSelected = true`
- 不存在 → 两个分类不进入树；**左侧筛选栏对应项一并隐藏（v0.10，不再灰显禁用）**，
  组内分类全隐藏时「懒人插件」组标题与分隔线也一起隐藏
- **禁止**把 `interface\LM`（枫影）或 `interface\lm#data` 当作懒人插件

### 5.3 R-03 错误账号

`userdata\{账号}\` 下不存在任何**子目录**（无大区层）→ `InvalidAccount`：

- 标红 `#C62828`，整行浅红底 `#FDECEA`，左侧 2px 红色竖条
- 说明文字「无区服文件 · 错误账号」
- **默认勾选 = true**（v0.2 变更）
- 汇总确认页单独列一组「错误账号（N）」，避免混在正常项里被忽略
- 实测样本：`user@qq.com`

排除项（不算账号）：`userdata` 根下的 `*.dat` / `*.ini` 文件，以及 `fight_stat` 这类非账号目录（判定依据：名字在保留名单内，或含 `config.jx3dat` 之类特征文件）。

### 5.4 R-04 过期区服

- 白名单：`{"电信区", "双线区", "无界区"}`，配置于 `config\regions.json`
- `userdata\{账号}\{大区}\` 名不在白名单 → `LegacyRegion`
- 橙色角标「过期区服」，行内灰色列出过期区名（如 `电信五区、双线一区`），勾选时弹 §6.3 确认框
- **v0.5 起**：角色数据不再有独立的大区节点，判定上移到账号行 —— 只要账号下存在任一个过期大区，整个账号行就带 `LegacyRegion`

### 5.4b R-04b 角色数据以账号文件夹为整体（v0.5）

```
userdata\{账号}\            ← 清理单位（勾选这一行）
    ├─ {角色名}（空目录，无区服文件）→ 错误账号，标红
    ├─ {大区}\{服务器}\{角色}\  ← 只用于①收集角色名 ②判定过期区服，不再作为可选节点
```

- 角色名 = 第三层目录名去重，用于行后灰色括号 `（角色1、角色2…）`，超 3 个折叠 `+n`
- 扫描时仍会遍历三层目录，但只为「凑角色名 + 查过期大区」，不生成子节点
- 目标条目 = 账号目录本身（`IsDirectory = true`），整目录删除
- 时间窗口按账号目录的 `Max(LastWriteTime)` 判定
- **v0.6 例外**：账号下存在过期大区时该行**可展开**，子节点只列过期大区（有效大区不生成节点），
  可以只勾选这几个大区而不动整个账号。展开节点带同一个 `LegacyRegion` 标志，勾选时同样触发确认弹窗

### 5.5 R-05 云团监分组

目录：`interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote\`

```
groupKey(name):
  lower = name.ToLowerInvariant()
  ".meta.jx3dat" → 去掉该后缀
  ".jx3dat"      → 去掉该后缀
  其它           → 忽略（不入组）
```

- 一组 = `r-<hash>.jx3dat` + `r-<hash>.meta.jx3dat`
- 只显示组，不显示单文件；组的体积/时间 = 组内文件的合计与 `Max(LastWriteTime)`
- 组名是哈希（`r-269249a6348be0bf`），**v0.5 起在组名后加灰色括号时间** `（2026-10-24 19:55）`，取组内最近修改时间（本地时区）
- 只有一半文件的组标记 `Incomplete`，仍可选
- 「保留最近 N 组」按组时间倒序保留前 N 组（与文件时间筛选取并集保护）
- **勾选前**首次进入勾选状态时弹提示（原文照抄）：

  > 若对团队监控数据有本地修改，请提前导出及备份

  确认后才生效；取消则回滚勾选。同一会话内不重复弹。
- `team_mon\local.jx3dat`、`metalist.jx3dat`、`audio\` **不在清理范围**，且 `local.jx3dat` 列入保护名单

### 5.5b R-07 金团记录（v0.5 新增）

目录：`interface\my#data\{gid}@zhcn_hd\userdata\gkp\`

| 文件 | 处理 |
|---|---|
| `2021-08-07-21-45-33_25人普通白帝江关.gkp.jx3dat` | 可清理（匹配 `^\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}_`） |
| `current.gkp.jx3dat` | **永远不动**，不计入体积、不参与时间窗口 |

- 枚举用宽 glob `*.jx3dat` + 正则精筛。不用 `*.gkp.jx3dat`：Win32 通配对复合后缀的匹配不可靠
- 按账号聚合，一行一个账号，不展开到单个团本记录（实测 40 个账号合计 809 文件 / 4.62 MB，单文件都很小）
- 实测：40 个账号都有 `gkp` 目录，其中 14 个账号存有历史记录
- 默认不勾选（删掉后游戏内金团记录列表会清空，属于用户自己的账目数据）

### 5.6 R-06 旧版团监整目录清理

- 判定：`interface\my#data\!all-users@zhcn_hd\userdata\TeamMon\` 存在（名字按 OrdinalIgnoreCase，注意 `TeamMon` 与 `team_mon` 是两个不同目录）
- 作为一个独立节点「旧版团监数据（已弃用）」，粒度为**整目录**（v0.10：标题后的「（已弃用）」用黄色小字）
- **存在才勾选（v0.6）**：目录不存在时整条分类不出现（也不会出现在左侧筛选栏），
  存在时默认勾选，风险等级「低」，说明：「V4 客户端已改用 `team_mon` 目录，此为旧版本残留」
- 整目录分类不再生成同名的子节点，分类行本身就是唯一可勾选条目
- 执行时删除 `TeamMon\` 及其全部内容（`Data\`、`Meta\`、`MetaList.jx3dat`）
- 不需要按组选择，也不弹备份提示（与 C-05 明确区分）

### 5.8 R-08 顶级分类可勾选（v0.6）

- 分类行（聊天记录 / 战斗记录 / …）的复选框**可点**，不再锁定
- 三态语义：全选 / 部分（子项有的勾有的没勾）/ 未选，`TreeItemVm` 构造时按子项状态初始化
- 勾选分类 = 级联勾选其下全部条目；取消 = 级联取消
- 组标题行（茗伊插件 / 懒人插件 / 游戏数据）**不显示复选框**，只作分段，避免误操作整组
- **整棵树没有「选中」语义**（v0.11）：交互一律落在复选框上，`TreeViewItem` 改用自写
  `ControlTemplate`（展开箭头 + `ContentPresenter` + `ItemsPresenter`），模板里**完全不引用
  `IsSelected`**，点组标题这类不可勾选的行也不会再整行变蓝
  - 注意：只覆盖 `SystemColors.HighlightBrushKey` 不够 —— Aero2 的 TreeViewItem 选中底色用的是
    主题内的静态画刷（`TreeViewItem.SelectedActive.Background` 一类），改系统色键拦不住，
    必须在模板层面不画
  - 顺带解决默认模板把 Header 塞进 `Auto` 列导致 `HorizontalContentAlignment=Stretch` 失效的老问题
    （现在显式取 `Left`，保持体积列紧跟内容、右对齐维持原样）
- 批量勾选带出过期区服时，`OnNodeCheckChanged` 会收集本次勾选涉及的全部 `LegacyRegion` 节点
  合并成**一次**确认弹窗；取消则回滚本次勾选
- 「全选」按钮逐组下发，同样受上述确认流程保护

### 5.9 R-09 保留策略与时间窗口的叠加（v0.8）

五类数据各有保留规则，其余分类不保留：

| 分类 | 设置项 | 规则 | 默认 |
|---|---|---|---|
| 聊天记录 | N | 保留最近 N 个 `.db`（每次登录生成一个） | 2 个 |
| 战斗记录 | X | 保留最近 X 天内修改的文件 | 30 天 |
| JCL战斗日志 | Y | 保留最近 Y 天内修改的文件 | 30 天 |
| 金团记录 | Z | 保留最近 Z 天内修改的文件 | 90 天 |
| 云团监数据 | M | 保留最近 M 组（按组修改时间倒序取前 M） | 5 组 |

判定顺序：**先保留、再看时间窗口**。

```
files（按 LastWriteTimeUtc 倒序）
  ├─ 前 KeepCount 个             → 保留，永不勾选，行后标注「保留最近 N 个 / N 天」
  ├─ 其余中 早于等于截止时间     → 可清理（生成 Target）
  └─ 其余中 晚于截止时间         → 被过滤，计入 ScanResult.Filtered，界面提示「已过滤 N 项，约 XX MB」
```

两个容易踩的点：

1. **条数规则是前缀，天数规则也是前缀**（输入已倒序，`≥ now-days` 的那段一定连续），
   但「可清理 / 被过滤」在剩余部分里是**交错**的，所以 `RetentionPolicy.Split` 返回下标集合而不是条数。
   v0.7 用 `Skip(keep).Take(cleanCount)` 取过文件，结果把该过滤的拿了进来 —— 由单测
   `可清理与可过滤的条目在剩余部分里交错时下标正确` 锁死。
2. **保留天数 == 时间窗口天数**时，剩余项全部可清理（边界值 `<=` 判为可清理，`>=` 判为保留，
   保留下来的本来就不参与判断），不会出现「既不保留也不清理」的空档。

---

### 5.10 R-10 运行环境自检（v0.9）

**要不要校验**：要，但要先认清一个事实 ——

> 框架依赖发布（`SelfContained=false`）时，`.NET` 桌面运行时**完全缺失**会导致 apphost 在托管代码
> 执行前就失败，进程内的任何检查都不会运行。所以「校验」分两层，缺一不可：

| 层 | 手段 | 能覆盖的场景 |
|---|---|---|
| 发布层（根本解） | `portable-selfcontained.pubxml`：自包含 + 单文件 | 目标机没有 .NET。运行时已打包，问题不存在 |
| 进程层（本节的 R-10） | 启动最早期自检 | 系统过旧、位数不对、被 roll-forward 到更高/更低运行时、私有 DOTNET_ROOT |

**判定表**（`RuntimeRequirements.Evaluate`，纯函数）：

| 编号 | 级别 | 条件 | 处理 |
|---|---|---|---|
| `E-OS-NONWIN` | 错误 | 非 Windows | 弹窗 → 退出码 2 |
| `E-OS-VER` | 错误 | Windows < 10.0.14393（.NET 10 / WPF 下限） | 弹窗 → 退出 |
| `E-ARCH-BITS` | 错误 | 进程非 64 位（被 32 位重定向 / 拷贝错文件） | 弹窗 → 退出 |
| `E-RT-OLD` | 错误 | 框架依赖且运行时大版本 < 10 | 弹窗 + 下载按钮 |
| `W-OS-W10` | 提示 | 10.0.22000 以下 | 圆角等 Win11 视觉不生效，放行 |
| `W-ARCH-EMU` | 提示 | ARM64 系统 + x64 进程 | 模拟执行会变慢，放行 |
| `W-ROLLFWD` | 提示 | 框架依赖且运行时大版本 > 10 | 借用了更高版本运行时，放行 |
| `W-RT-NOTFOUND` | 提示 | 常规位置没探到 `Microsoft.WindowsDesktop.App` | 可能装在私有目录，**不拦** |
| `I-SC` / `I-FDD` | 信息 | 自包含 / 已检测到桌面运行时 | 写进诊断报告 |

`W-RT-NOTFOUND` 只提示不拦截是刻意的：能跑到自检说明 WPF 已经加载，运行时必然存在，
探不到只说明装得偏；在这里拦人等于制造假故障。

**发布形态怎么判准**（判错的代价是把能跑的自包含程序拦下来）：

1. 优先读编译期烧进程序集的元数据 —— `App.csproj` 里
   `<AssemblyMetadata Include="DeploymentMode" Value="$(SelfContained ? …)" />`，
   运行时用 `assembly.GetCustomAttributes<AssemblyMetadataAttribute>()` 取；
2. 元数据缺失（例如测试宿主）才退化为比较 `typeof(object).Assembly.Location` 与程序目录；
3. 都拿不到就记 `Unknown`，只给一条提示，绝不阻断。

单文件发布下 `Assembly.Location` 为空，若只靠路径推断，自包含单文件会被误判成框架依赖 —— 这正是引入
元数据标记的原因。

**探测内容**（`RuntimeProbe`，只读、无副作用、全部 try/catch，失败项按未知处理）：
系统版本与架构、进程位数与架构、运行时版本、`dotnet` 根目录（当前运行时目录反推 + `DOTNET_ROOT`
+ `Program Files` / `Program Files(x86)` / `LocalAppData`）、各根目录下 `shared\Microsoft.WindowsDesktop.App`
的版本列表。

**`RollForward=Major`**：框架依赖版在只装了更高大版本运行时的机器上也能启动，自检记为 `W-ROLLFWD`。

**发布命令与单文件压缩的绑定**（v0.12）：`EnableCompressionInSingleFile` 只在
`$(SelfContained) == true` 时打开。框架依赖版的运行时来自目标机，单文件捆绑包里没有可压缩的
运行时文件，SDK 会直接报 `NETSDK1176: 仅在发布独立应用程序时才支持在单个文件捆绑包中进行压缩`。
条件化之后，`dotnet publish -p:SelfContained=false -o publish` 与自包含 profile 两条命令都成立：

| 命令 | 发布目录产物 | 改名后（`dist\` 内一并存放） | 体积 | 目标机 |
|---|---|---|---|---|
| `-p:PublishProfile=portable-selfcontained` | `dist/portable/JX3Cleaner.exe` | `JX3Cleaner-v0.12-selfcontained.exe` | 62.1 MB | 免装 .NET |
| `-c Release -p:SelfContained=false -o publish` | `publish/JX3Cleaner.exe` | `JX3Cleaner-v0.12-framework-dependent.exe` | 724 KB | 需 .NET 10 桌面运行时 |

两个单文件同名，所以发布后要各改一次名才能并存（v0.12 起 `dist\` 同时留两份）。
框架依赖版从 424 KB 长到 724 KB，是 exe 图标（7 档尺寸 ICO）同时以 Win32 资源与 WPF `Resource`
各存一份所致 —— 图标只进程序集一次是做不到的，两者用途不同（资源管理器 vs `Window.Icon`）。

**平台声明**（v0.12）：`Core` 目标框架仍是 `net10.0`，但用
`<AssemblyAttribute Include="SupportedOSPlatformAttribute">windows</AssemblyAttribute>`
声明整只程序集仅 Windows 可用 —— Core 里用到注册表、Shell COM、回收站，逐处标
`[SupportedOSPlatform]` 只会变成噪音。`App` / `Diag` / `Tests` 统一用 `net10.0-windows`
目标（调用方也必须声明同一平台），于是 `CA1416` 清零。

**出口**：`RuntimeCheckDialog`（错误时只给「退出」，可复制诊断文本、可跳下载页）；
提示不弹窗，只在设置页底部「运行环境」一行显示摘要，悬停看完整报告。`Diag env` 打印同一份报告。

---

## 6. 界面设计

### 6.1 导航

两个页签 + 一个独立设置窗口（v0.8 收敛：概览页取消，设置改为弹窗）：

| 位置 | 内容 |
|---|---|
| 页签「数据清理」 | 主功能页（§6.2） |
| 页签「截图与云图」 | 图片浏览与清理（§6.4） |
| 顶栏「设置」按钮 → 设置窗口 | 客户端路径、默认清理方式、五项保留策略（§6.6） |

### 6.2 数据清理页

```
┌────────────────────────────────────────────────────────────────┐
│ 客户端 [D:\…\JX3                    ] ✓已校验  [设置] [扫描]     │
├─────────────┬──────────────────────────────────────────────────┤
│ 数据类型     │  ▬ 茗伊插件                                      │
│ 茗伊插件     │  ▾ ■ 聊天记录  interface\my#data\…\chat_log 750 MB│
│  ☑ 聊天记录  │    ■ 角色甲    保留最近 2 个          16.2 MB     │
│  ☑ 战斗记录  │    □ 角色乙  保留最近 30 天         44.0 KB     │
│  ☑ JCL战斗日志│  ▸ □ 云团监数据 …\team_mon\remote      65.6 MB   │
│  ☑ 金团记录  │  ■ 旧版团监数据  整目录 …\TeamMon       2.5 MB   │
│  ☑ 云团监数据│                                                   │
│  ☑ 旧版团监  │  ▬ 游戏数据                                       │
│ ─────────── │  ▾ ■ 角色数据  userdata                11.3 MB    │
│ 懒人插件     │    □ 13800000000（角色丙@隐客、角色丁） 40 KB   │
│  □ 懒人插件  │    □ 13900000000  无区服文件·错误账号      88 B    │
│  □ 懒人插件数据│  ▸ □ 家园蓝图  homelanddir\Blueprints  89.1 KB  │
│ ─────────── │  ▸ □ 染发方案  hairdyeingdata          136 B     │
│ 游戏数据     │  ■ 设计站图片  sharestationdata         9.9 MB   │
│  ☑ 角色数据  │                                                   │
│ ─────────── │  （截图 / 幻境云图 不在本页，见 §6.4）           │
│ 文件时间     │                                                   │
│ [只清理90天前▾]│                                                 │
│ 已过滤 501 项，│                                                 │
│ 约 343.7 MB  │                                                   │
│ ─────────── │                                                   │
│ 清理方式     │                                                   │
│ ● 回收站 ○删除│                                                   │
│ [全选][全部取消]│                                                │
├─────────────┴──────────────────────────────────────────────────┤
│ 已选 252 项 · 687.5 MB                  [●直接删除] [开始清理]  │
└────────────────────────────────────────────────────────────────┘
```

> **v0.7**：截图（C-08）与幻境云图（C-09）从本页移除，改在「截图与云图」页勾选清理。
> 「开始清理」条也从窗口级下移到本页内部，与图片页各自的底栏互不影响。
>
> **v0.10**：C-03 / C-04 / C-06 标题后带黄色「（已弃用）」；目录不存在时左栏分类项与右栏分类节点**都直接隐藏**
> （本机未装懒人插件，实际截图中「懒人插件」整组不出现，只有「旧版团监数据（已弃用）」带标记）。
>
> **v0.8**：顶栏的「浏览…」换成「设置」（定位统一走设置页），路径框改为只读；
> 「文件时间」下拉框正下方新增过滤提示 `已过滤 N 项，约 XX MB`（仅在非「全部」时出现）；
> 「全选 / 全部取消」并排一行以省出垂直空间；「打开日志目录」按钮移除（不做日志查看）。

视觉规范：

| 元素 | 规范 |
|---|---|
| 组标题行 | 14px / 600 / `#1F1F1F`，底 `#F5F4F1`，**无复选框**，只作分段 |
| 分类标题 | 13px / 500 / `#1F1F1F`，**复选框可点**，三态（全选 / 部分 / 未选） |
| 分类相对路径 | 11px `#9A9A9A` 带下划线，`Cursor=Hand`，点击在资源管理器打开；**细项不显示** |
| 账号节点 | 13px / 500 / `#1F1F1F`，只有**角色数据**带 `（角色1、角色2…）` |
| 括号与角色名 | 12px / 400 / `#8C8C8C`，全角括号，`、`分隔，超 3 个折叠 `+n` |
| 茗伊各类行 | 只有账号名 + 灰色备注，**不显示括号角色名、不显示 global_id** |
| 云团监组 | 组名后灰色括号时间 `（yyyy-MM-dd HH:mm）` |
| 大区/服务器/角色 | 不再生成节点；**仅当账号下有过期区服时**，展开子节点列出过期大区 |
| 错误账号 | `#C62828` + 行底 `#FDECEA` + 左 2px 红条 |
| 过期区服 | 胶囊角标：底 `#FFF4E5` 字 `#B26A00` 12px，行内灰字列出区名 |
| 组/目录体积 | 12px `#8C8C8C`，自动进位 GB/MB（**不强制右对齐，v0.5 明确放弃**） |
| 开始清理按钮 | **常驻且始终可点**；未勾选时点击提示「还没有勾选任何可清理的数据」 |
| 底栏归属 | 数据清理页与截图页**各有自己的底栏**，窗口级不再有共用底栏（v0.7） |

顶部是 `数据清理` / `截图与云图` 两个页签，页签下的内容区各自独立。

XAML 关键点（角色名小号灰字 + 可点路径）：

```xml
<TextBlock Text="{Binding Title}" FontSize="{Binding TitleFontSize}" FontWeight="{Binding TitleWeight}" />
<TextBlock Text="{Binding RoleText}" FontSize="12" Foreground="#8C8C8C"
           Visibility="{Binding HasRoles, Converter={StaticResource BoolToVis}}" />
<TextBlock Text="{Binding RelativePathText}" FontSize="11" Foreground="#9A9A9A"
           Cursor="Hand" TextDecorations="Underline"
           MouseLeftButtonUp="RelativePath_Click"
           Visibility="{Binding HasRelativePath, Converter={StaticResource BoolToVis}}" />
```

### 6.3 弹窗

**A. 过期区服确认**

> **清理范围包含过期区服数据**
> 以下区服目录不属于当前客户端的有效区服（电信区 / 双线区 / 无界区），可能来自区服合并前的旧版本：
>
> | 账号 | 明细 |
> |---|---|
> | 13800000001（角色庚） | 电信五区 · 3 服务器 / 7 角色 |
>
> 这些目录里可能仍有未迁移的按键、界面与宏配置。请先在游戏内确认角色已完成数据转移或合并。
>
> ☐ 我确认上述角色的数据已完成转移，或已不再需要
> `[取消]` `[确认清理]`（未勾选时禁用）

**B. 云团监备份提示**（勾选前）

> **清理云团监数据**
> 若对团队监控数据有本地修改，请提前导出及备份。
> `[取消]` `[我知道了]`

（正文首句必须是上面这句原文，不改字。）

**C. 直接删除二次确认**

> **即将直接删除，无法恢复**
> 方式为调用 `cmd.exe del`，文件不进入回收站。共 N 项 / M 个文件 / X GB。
> ☐ 我知道该操作不可撤销
> `[取消]` `[确认删除]`（红色主按钮）

**D. 手动定位前置提示**（v0.8）

> **手动定位**
> 请定位至旗舰客户端zhcn_hd目录
> `[确定]` →（确定后才打开文件夹选择器，选择器标题同文案）

（这条是设置页「手动定位」按钮的固定前导提示，文案按需求原文，不改字。）

**E. 设置页保存拦截**（v0.8）

> **客户端地址未就绪**
> 请先定位客户端地址，校验通过后才能保存。

> **校验失败**
> 客户端目录已失效，请重新定位。

### 6.4 截图与云图页（v0.7 已承接清理）

```
┌──────────────────────────────────────────────────────────────────┐
│ [截图][幻境云图]  G://…//bin//zhcn_hd//ScreenShot   [全选][全部取消][打开目录][刷新] │
├──────────┬───────────────────────────────────────────────────────┤
│ 时间导航  │ 2026 年 10 月   1 项 · 1002.0 KB · 视频 1             │
│          │ ┌────────┐                                            │
│ 2026-10  │ │ ☑ 缩略 │  ▶视频                                     │
│ 1 项·1 MB│ ├────────┤                                            │
│ 2026-09  │ │…mp4    │                                            │
│ 1 项·858K│ │1002 KB │                                            │
│ 2026-08  │ └────────┘                                            │
│ 2 项·2.2M│ 2026 年 09 月   1 项 · 858.2 KB                       │
│ 2026-07  │ ┌────────┐                                            │
│ 1 项·1.6M│ │ ☐ 缩略 │                                            │
│   …      │ └────────┘                                            │
├──────────┴───────────────────────────────────────────────────────┤
│ 已选 4 项 · 4.0 MB       共 66 项（图片 65·视频 1）·106.9 MB·29 个月 │
│ 共 4 个文件 · 其中视频 1 个      [●回收站] [○直接删除] [清理选中]   │
└──────────────────────────────────────────────────────────────────┘
```

**布局**：左侧 152px 时间导航 + 右侧缩略图网格，顶部工具栏，底部选中统计与清理条。

**按月分组**

- 分组键优先取**文件名里的拍摄时间**（`^\d{4}-\d{1,2}-\d{1,2}[_-]…`），取不到才退回文件 mtime。
  原因：文件被复制 / 从备份还原后 mtime 会变，文件名才是真正的拍摄时刻。
- 组头显示 `2026 年 10 月` + `N 项 · 体积 · 视频 M`（有视频才带第三段）。
- 排序同一把尺子：按 `Timestamp` 倒序，同分时按文件名倒序。

**时间导航**

- 左侧列出全部月份，形如 `2026-10` + 下方灰字 `1 项 · 1002.0 KB`，按月倒序。
- 点击 → `ScrollViewer.ScrollToVerticalOffset(该月组头的行号)`；`CanContentScroll=true` 时偏移量就是行号，不需要像素换算。
- 定位动作同时挂在 `SelectionChanged` 和 `ListBoxItem.PreviewMouseLeftButtonUp`：
  后者的原因是「已选中项再点一次」不会触发 `SelectionChanged`，只靠前者会出现点了没反应。
- 右键月份：`全选本月` / `取消本月`。

**勾选清理**

- 每张卡片左上角有复选框；**单击卡片**也能切换勾选，**双击**用系统默认程序打开。
- 双击时会撤销第一次点击造成的勾选翻转（记下点击前的值再还原），避免「想打开却切成未选」。
- 右键菜单：用系统默认应用打开 / 打开所在路径 / 复制完整路径 / 勾选此项 / 取消勾选。
- 底栏：`已选 N 项 · 体积` + 目录总览 + 清理方式（回收站 / 直接删除）+ `清理选中`。
- 选「直接删除」时按钮转红、文案变「直接删除选中」。
- 点清理后把选中项转成 `ScanNode` 抛给主窗口，**复用** `CleanPlanBuilder` + `PathGuard` + 审计日志，
  与数据清理页完全同一条执行链路（含「游戏运行中禁止清理」与二次确认）。

**虚拟化（行级）**

- 数据结构：`MediaRowVm`（月份标题行 / 卡片行）+ `VirtualizingStackPanel`。
  一行最多 `floor((可用宽度 - 16) / 188)` 张卡片（188 = 卡片 176 + 左右外边距）。
- `ItemsControl` 自己的 `ControlTemplate` 里放 `ScrollViewer`，`CanContentScroll=true`，
  `VirtualizationMode=Recycling`——这是虚拟化生效的前提，套在外层 `ScrollViewer` 里不会虚拟化。
- 缩略图**按需解码**：卡片的 `Loaded` 才请求 Shell 缩略图（3 并发信号量），不是一次性全解。
- 内存兜底：滚动时把可视行上下 40 行以外的缩略图释放掉（`TrimThumbnails`），
  否则一路翻到底会把整目录缩略图留在内存里。
- 实测：485 个文件 / 145 行，首屏只实体化 **55** 张卡片。
- 宽度变化时按「当前视口首项」为锚点重排行，不会跳回顶部。

**其它**

- 实时预览：`FileSystemWatcher`（`FileName | LastWrite | Size`）监听当前目录，500 ms 防抖后增量刷新；
  已存在条目的 ViewModel 复用，缩略图不重解码。
- 空目录显示「该目录下暂无图片或视频」；目录不存在显示「目录不存在」。
- 缺解码器 / 文件损坏 → 占位块显示扩展名，不影响勾选与清理。

### 6.5 清理方式控件（FR-10）

底栏两个单选，文字与外显颜色：

| 选项 | 外显 | 样式 |
|---|---|---|
| ① | **移动到回收站** | 默认选中，中性色；tooltip「删除到回收站，可从回收站还原」 |
| ② | **直接删除** | 选中时整块底色 `#FDECEA`、文字 `#C62828`、1px 红边；tooltip「调用 cmd.exe del，不进入回收站，删除后无法恢复」 |

- 选②后，「开始清理」按钮同步变红（底 `#C62828`、字白）
- 汇总确认页顶部出现红色警示条：「当前为直接删除模式，文件不会进入回收站」
- 说明文字用「直接删除」，括号内可小字注明 `cmd.exe del`，但主标签不用技术术语
- **v0.8**：清理方式以设置页的「默认清理方式」为初值；数据页与图片页互相联动，
  任一侧切换后另一侧跟随，避免「这页删回收站、那页直接删」

### 6.6 设置页（v0.8）

独立窗口 `SettingsWindow`（760 宽，`SizeToContent=Height`），七个设置项各占一行，
左列固定 196 宽标签，右列控件；底部左下角始终提示「保留数量填 0 表示不作保留（全部纳入清理范围）」，
右下角是唯一的主按钮 **保存**，点击后落盘 `config\settings.json` 并关闭设置页。

| 行 | 标签 | 控件 |
|---|---|---|
| 1 | 客户端地址 | 真实地址只读框 + `扫盘` + `手动定位`，下方一行校验状态（`已校验 · …\bin\zhcn_hd` / 红色失败原因） |
| 2 | 默认清理方式 | 两个单选：移动到回收站（可还原） / 直接删除（cmd.exe del，选中变红） |
| 3 | 聊天记录默认保留最近 | `NumberStepper` 步长 **1**，单位「个」，范围 0–60 |
| 4 | 战斗记录默认保留最近 | 步长 **5**，单位「天」，范围 0–3650 |
| 5 | JCL战斗日志默认保留最近 | 步长 **5**，单位「天」 |
| 6 | 金团记录默认保留最近 | 步长 **5**，单位「天」 |
| 7 | 云团监数据默认保留最近 | 步长 **1**，单位「组」，范围 0–200 |

**加减框**（`Controls/NumberStepper`）：`[−] [数值] [+] 单位`，数值用 `TextBlock` 渲染而不是输入框，
从根上排除「用户乱输导致解析失败」的分支；`Value` 是 `TwoWay` 依赖属性，并在 `CoerceValue` 里按 `[Min, Max]` 收敛，
到达边界时对应按钮直接禁用。

**交互约定**

- `手动定位`：**先弹提示框「请定位至旗舰客户端zhcn_hd目录」**，确认后才打开文件夹选择器（标题同文案）。
- `扫盘`：先走注册表 + 常见安装路径，再对固定磁盘做 5 层深度的目录搜索（只认 `…\bin\zhcn_hd\bin64`，
  跳过 `Windows` / `ProgramData` / `AppData` / 无权限目录与 reparse point），扫描期间按钮禁用并实时回显进度。
- 校验失败不写入任何持久化状态：`ClientLocator` 直接调用，不经过 `CleanerService.Validate`（那个会立即存盘）。
- 保存时再校验一次，防止这期间目录被移动或卸载。

**首次启动流程**

```
OnStartup → Service.Initialize()
          → NeedsSetup？（没有配置 / 配置里目录已失效）
              ├─ 是 → SettingsWindow(firstRun: true) 模态
              │        ├─ 保存且 Root 有效 → 继续
              │        └─ 直接关窗 → Shutdown()
              └─ 否 → MainWindow
```

启动期间 `ShutdownMode` 临时设为 `OnExplicitShutdown`（否则设置窗关闭瞬间「最后一个窗口关闭」会退出进程），
主窗口 `Show()` 之后再改回 `OnMainWindowClose`。

### 6.7 Win11 圆角（v0.8）

两层：

1. **窗口级**：`Interop.WindowChrome.EnableRoundedCorners(Window)` 在 `SourceInitialized` 后调
   `DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE=33, DWMWCP_ROUND=2)`。
   Win10 及更早系统该调用返回错误码，忽略即可（保持原外观）。三个窗口（主窗口 / 设置 / 确认弹窗）都套。
2. **控件级**：`Themes/Win11.xaml` 的隐式样式覆盖 Button / TextBox / ComboBox / CheckBox / RadioButton /
   TabItem / ScrollBar / ListBoxItem，圆角半径统一为：控件 6、小标签 4、卡片与面板 8。
   - 悬停/按下用**半透明覆盖层**（`#14000000` / `#26000000`），这样深色主按钮与浅色次按钮共用一个模板；
   - 复选框三态：勾选与半选都填充 `#1F1F1F`，分别显示对勾与短横，符合 Win11 语义；
   - 滚动条改成 12px、无箭头、圆角拇指。

---

## 7. 技术选型

| 方案 | 回收站 | 图片预览 | 体积 | 效率 | 结论 |
|---|---|---|---|---|---|
| **C# .NET 10 + WPF** | `IFileOperation` 一等公民 | `Image` + 解码降采样成熟 | 框架依赖 ~1 MB | 高 | ✅ 已采用 |
| C# + WinUI 3 | 同上 | `ItemsRepeater` 更强 | 需 App SDK | 中 | 备选 |
| Tauri | `trash` crate | Web 前端灵活 | ~8 MB | 中 | 跨平台时考虑 |
| Electron | `shell.trashItem` | 最强 | 100 MB+ | 高 | ❌ 太重 |
| Python + PySide6 | `send2trash` | 一般 | 40 MB+ | 高 | ❌ 误报风险 |

组合：**.NET 10 + WPF**（不引入 MVVM 框架，直接用 `INotifyPropertyChanged` 手写视图模型，减少依赖），`dotnet publish -r win-x64 /p:PublishSingleFile=true` 绿色发布。

发布形态（v0.9 定案）：**对外分发走自包含**（`portable-selfcontained.pubxml`，61.7 MB 单文件，目标机免装 .NET）；
框架依赖（约 1 MB 级别）只用于开发机自用。原因见 §5.10 —— 框架依赖版在没装运行时的机器上，
连自检代码都跑不到。
应用启动极早期设置 `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly`，强制软件渲染以换取在老旧显卡 / 远程桌面 / 虚拟机上的稳定性。

图片页补充：不引入第三方图片/视频库。图片走 WPF `BitmapImage` + `DecodePixelWidth`；**含 mp4 的统一走 Shell `IShellItemImageFactory` 取首帧**；虚拟化用 `VirtualizingStackPanel` + 自定义 `VirtualizingWrapPanel`（约 150 行）。播放一律外抛系统默认播放器，应用内不集成媒体播放组件。

---

## 8. 架构与模块

```
JX3Cleaner.slnx
├─ src/
│  ├─ JX3Cleaner.Core/                 无 UI 依赖，可单测
│  │   ├─ Config/                      CategoryCatalog / AppSettings / KeepRule / PortablePaths / RegionConfig
│  │   ├─ Discovery/                   ClientLocator（归一化 / 校验 / 扫盘）
│  │   ├─ Scanning/                    ScanEngine / ScanOptions / RetentionPolicy / FilterStats
│  │   ├─ Rules/                       AccountResolver / TeamMonGrouper / MediaNaming
│  │   ├─ Planning/                    CleanPlanBuilder + PathGuard
│  │   ├─ Executing/                   RecycleBinExecutor / DirectDeleteExecutor / ProcessGuard
│  │   ├─ Interop/                     IFileOperation COM 互操作（手写 vtable）
│  │   └─ Auditing/                    AuditWriter
│  ├─ JX3Cleaner.App/                  WPF 界面
│  │   ├─ MainWindow                   顶栏 + 两个页签
│  │   ├─ MediaBrowser                 截图与云图页（含清理）
│  │   ├─ SettingsWindow               设置页（v0.8）
│  │   ├─ Controls/NumberStepper       加减框（v0.8）
│  │   ├─ Themes/Win11.xaml            圆角控件模板（v0.8）
│  │   ├─ Interop/                     ShellThumbnail / WindowChrome / WindowShot
│  │   ├─ ViewModels/                  TreeItemVm / MediaItemVm / MediaRowVm
│  │   └─ ConfirmDialog                三处二次确认
│  └─ JX3Cleaner.Diag/                 控制台诊断：scan / plan / selftest
└─ tests/JX3Cleaner.Tests/             xUnit 101 条
```

| 模块 | 职责 | 测试要点 |
|---|---|---|
| ClientLocator | 归一化 + 硬/软校验 | 选 `zhcn_hd` 与选 `JX3` 两条路径都能过 |
| MyDataScanner | 枚举 `*@zhcn(_hd)`，排除 `#`/`!` | 后缀两种、大小写混合 |
| AccountResolver | R-01 映射 | 固定目录不误判、纯数字过滤 |
| TeamMonGrouper | R-05 分组 | 150 文件 → 75 组；半组容错 |
| UserDataScanner | 四级树 + 错误账号 | `fight_stat` 不当账号；根级 .dat 不算 |
| PathGuard | 越界拦截 | junction、`..`、根目录、系统属性 |
| DirectDeleteExecutor | 命令构造与结果校验 | 含 `&`/空格路径、只读、隐藏 |

核心模型：

```csharp
public enum CleanCategory
{
    ChatLog, FightStat, LazyPlugin, LazyPluginData,
    CloudTeamMon, LegacyTeamMon, Blueprint,
    Screenshot, Dcim, RoleData, HairDye, ShareStation
}

[Flags] public enum NodeFlags { None=0, InvalidAccount=1, LegacyRegion=2, UnknownOwner=4, IncompleteGroup=8, Protected=16 }

public sealed record CleanNode
{
    public required CleanCategory Category { get; init; }
    public required string FullPath { get; init; }
    public NodeKind Kind { get; init; }            // File / Directory / Group
    public long? GlobalId { get; init; }
    public string? AccountDisplay { get; init; }
    public string? Region { get; init; }
    public string? Server { get; init; }
    public string? Character { get; init; }
    public IReadOnlyList<string> GroupFiles { get; init; } = [];  // 云团监组文件
    public long SizeBytes { get; init; }
    public int FileCount { get; init; }
    public DateTime LastWriteUtc { get; init; }
    public RiskLevel Risk { get; init; }
    public NodeFlags Flags { get; init; }
    public bool DefaultSelected { get; init; }
}
```

---

## 9. 关键实现

### 9.1 回收站

```csharp
var op = (IFileOperation)new FileOperationCoClass();
op.SetOperationFlags(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI);
foreach (var path in plan.Items)
{
    var item = SHCreateItemFromParsingName<IShellItem>(path);
    op.DeleteItem(item, null);
}
op.PerformOperations();
```

注意：先清 `ReadOnly`；COM 对象不跨线程复用；回收站配额不足或非 NTFS 卷会退化为永久删除，需前置提示。

### 9.2 直接删除（cmd.exe del）

```csharp
static ProcessStartInfo Build(string path, bool isDir)
{
    var psi = new ProcessStartInfo("cmd.exe")
    {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        StandardOutputEncoding = Encoding.GetEncoding(936)
    };
    psi.ArgumentList.Add("/d");   // 跳过 AutoRun
    psi.ArgumentList.Add("/c");
    psi.ArgumentList.Add(isDir
        ? $"del /f /q /s \"{path}\\*\" & rmdir /s /q \"{path}\""
        : $"del /f /q \"{path}\"");
    return psi;
}
```

必须写进注释的坑：

1. `del` 只能删文件，目录必须配 `rmdir /s /q`。
2. 整条命令作为**一个参数**交给 `ArgumentList`，绝不拼 `Arguments` 字符串。
3. `del` 对不存在的文件也返回 0 → 必须「执行前记录 → 执行后复核」来判定成功。
4. 隐藏/系统属性 `del /f` 仍会失败，需先 `attrib -s -h -r`。
5. stdout 是 GBK，读流用 936 编码。

### 9.3 路径守卫

```csharp
public bool IsSafe(string candidate)
{
    var full = Path.GetFullPath(candidate);
    var root = Path.GetFullPath(_zhcnDir).TrimEnd('\\') + "\\";
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
    if (full.Length <= root.Length + 1) return false;
    if (Protected.Contains(full.TrimEnd('\\'))) return false;
    var a = File.GetAttributes(full);
    if (a.HasFlag(FileAttributes.ReparsePoint) || a.HasFlag(FileAttributes.System)) return false;
    return true;
}
```

保护名单（永不可删）：

```
zhcn_hd / interface / my#data / userdata
userdata\fight_stat
!all-users@zhcn_hd\userdata\team_mon\local.jx3dat
!all-users@zhcn_hd\userdata\team_mon\metalist.jx3dat
!all-users@zhcn_hd\userdata\team_mon\audio
homelanddir / hairdyeingdata / sharestationdata（目录本身）
```

### 9.4 云团监分组

```csharp
static string? Key(string fileName)
{
    var n = fileName.ToLowerInvariant();
    if (n.EndsWith(".meta.jx3dat")) return n[..^12];   // ".meta.jx3dat".Length
    if (n.EndsWith(".jx3dat"))      return n[..^7];
    return null;
}
```

按 key 分桶 → 计算合计体积与最大时间 → 按时间倒序 → 应用「保留最近 N 组」。

### 9.5 缩略图（图片与视频统一）

图片可以用 `BitmapImage` 解码，但 **mp4 无法用 `BitmapImage` 读首帧**。因此统一改走 Windows Shell 缩略图接口 `IShellItemImageFactory::GetImage`——它对图片与视频一视同仁，视频由系统已安装的解码器取首帧。

```csharp
var factory = (IShellItemImageFactory)SHCreateItemFromParsingName(path);
factory.GetImage(
    new SIZE { cx = 320, cy = 240 },
    SIIGBF.SIIGBF_THUMBNAILONLY | SIIGBF.SIIGBF_BIGGERSIZEOK,
    out IntPtr hBitmap);
```

- `HBITMAP` → `BitmapSource` → `Freeze()` → 回 UI 线程赋值
- 失败（缺编解码器 / 文件损坏 / 格式不支持）返回 `null`，UI 显示占位块 + 扩展名后缀
- 后台 `Task.Run` 执行，并发上限 3（`SemaphoreSlim`）；不得在 UI 线程调用
- **按需解码（v0.7）**：卡片 `Loaded` 才请求，不是一次性把整目录排进队列
- **内存兜底（v0.7）**：`TrimThumbnails` 在滚动时释放可视行上下 40 行以外的缩略图，
  否则「视觉虚拟化、内存没虚拟化」，一路翻到底仍会吃满内存
- 容器回收用 `VirtualizationMode=Recycling`，避免反复构造卡片
- **视频只取首帧，全程不解码播放**；双击走 `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`
- 可选优化（v1.1）：缩略图落盘缓存到程序目录 `cache\thumbs`（便携约束，不用 `%LOCALAPPDATA%`），键为 `路径 + LastWriteTime`

---

## 10. 开发阶段

| 阶段 | 内容 | 人日 |
|---|---|---|
| **M0 结构核实** | 本机实测（已完成，见 §2）；`DCIM` 路径与媒体类型已确认，无剩余待验项 | 1 |
| **M1 扫描内核** | 骨架、DI、ClientLocator + 校验、四类扫描器、体积聚合 | 4 |
| **M2 规则引擎** | R-01~R-06、账号映射缓存、云团监分组 | 3.5 |
| **M3 主界面** | 数据清理页、四级树、筛选栏、样式规范 | 4 |
| **M4 图片页** | 缩略图虚拟化、日期分组、右键菜单、FileSystemWatcher | 3 |
| **M5 执行器与安全** | PathGuard、进程检测、回收站、直接删除、审计 | 3.5 |
| **M6 打磨发布** | 文案、图标、单文件打包、干净机验证 | 2 |

**合计约 21 人日**（v0.1 为 18，新增图片页与目录校验）。

交付点：

- **v0.5（M0–M2）**：CLI/调试窗口，能扫描、能导出、不能删 —— 用于验证规则。
- **v0.8（M5）**：功能完整，样式待打磨。
- **v1.0（M6）**：发布。

### 10.1 验收标准

1. 手动选择 `…\JX3` 与 `…\JX3\bin\zhcn_hd` 两种路径均能通过校验；选错目录给出明确阻断原因。
2. 本机 40 个账号的显示名与游戏内一致（抽查 ≥ 10 个）。
3. `user@qq.com` 被判为错误账号、标红、默认勾选。
4. 云团监 150 个文件渲染为 75 组，勾选时弹出备份提示。
5. 旧 `TeamMon` 节点存在且默认勾选，执行后整目录消失。
6. 截图页 65 项缩略图 1 s 内出图（含 mp4 首帧）；新增一张截图或一段录屏，1 s 内自动出现。
7. 双击图片调起默认看图程序；双击 mp4 调起本地默认播放器；应用内**不出现任何播放控件**。右键两项菜单分别打开文件与在资源管理器中定位选中。
8. 「直接删除」选中后按钮与底栏变红，且有二次确认。
9. 干跑清单与实际删除逐条一致。
10. 越界路径（`..`、junction、根目录）全部被拦截。

---

## 11. 测试

| 层级 | 方式 |
|---|---|
| 单元 | 规则、分组、路径守卫、保留策略、命名解析 → xUnit + 临时目录夹具（`TempClient`） |
| 集成 | `ScanEngine` 端到端：临时目录搭假客户端树跑全链路 |
| 手工 | 弹窗、样式、DPI 125%/150%、清理方式联动 |
| 破坏性 | 只读、占用、超长路径、中文与 `&`/`^` 特殊字符、无权限（`Diag selftest`） |

### 11.1 已落地用例（135 条）

`dotnet test tests/JX3Cleaner.Tests/JX3Cleaner.Tests.csproj` → 135 通过 / 0 失败。

| 文件 | 覆盖点 |
|---|---|
| `RetentionPolicyTests` | 无规则、条数前缀、天数前缀、边界值 0 / 超过总数、三段切分、交错下标、`FilterStats` 累计 |
| `RuntimeRequirementsTests` | 正常机器通过、自包含免装 .NET、探不到运行时不拦截、非 Windows / Win7 / 32 位 / 运行时过低拦截、1607 边界放行、Win10 只提示、ARM64 模拟、roll-forward、形态未知、诊断文本、一行摘要、元数据映射、探测不抛异常、框架路径反推根目录 |
| `KeepSettingsTests` | 五项默认值、分类→规则映射、越界收敛、`AppSettings` 往返、副本独立性、规则文案 |
| `AccountResolverTests` | `@zhcn_hd` / `@zhcn` / 大小写、非账号目录、枚举跳过 `!all-users` 与 `#区服`、玩家名解析、`+n` 折叠、固定子目录与纯数字目录排除、未知账号兜底 |
| `TeamMonGrouperTests` | 两级后缀取键、非 `.jx3dat`、成对归组、半组标记、按时间倒序、`local.jx3dat` 不在 `remote` |
| `CleanPlanBuilderTests` | 去重、父目录覆盖子文件、递归收集、越界拒绝、受保护文件拒绝 |
| `PathGuardTests` | 正常文件通过、越界拦截、`zhcn_hd` 与关键父目录拦截、云团监本地数据保护、`..` 规范化后仍拦截 |
| `CategoryCatalogTests` | 金团正则命中 / 排除 `current.gkp.jx3dat`、媒体类不进数据页、三组标题顺序、相对路径不以分隔符开头 |
| `MediaNamingTests` | 标准命名、不补零、非时间前缀、月份/日期/小时越界、`yyyy-MM` 月份键 |
| `ClientLocatorTests` | 三种输入归一化、非法输入、`bin64` 硬校验、无清理目录判失败、缺失目录只作软提示 |
| `ScanEngineTests` | 聊天记录按 N 保留、N=0 全清、战斗记录按 X 天、时间窗口过滤计数与体积、云团监按 M 组、错误账号默认勾选、过期区服子节点、`userdata` 保留目录、懒人插件存在性、已弃用标记随节点下发、媒体类跳过、蓝图时间过滤、总量汇总 |
| `CategoryCatalogTests`（在 `CleanPlanBuilderTests.cs` 内） | 金团正则、媒体类归属、三组标题顺序、**已弃用分类恰为 C-03 / C-04 / C-06**、已弃用分类均为整目录粒度且存在才出现、后缀文案固定、未被误标 |

边界样本（已含在用例里）：

- `12345@zhcn_hd`（只有固定空目录 audio/logs）
- `12345@zhcn`（旧后缀）
- `#电信区_示例服@zhcn_hd`（区服目录，须排除）
- `!all-users@zhcn_hd`（公共目录，须排除）
- `userdata\user@qq.com`（无大区 → 错误账号）
- `userdata\fight_stat` / `config` / `role_statistics`（非账号目录）
- `team_mon\remote` 只有 `r-a.jx3dat` 无 `.meta` 的半组
- `2021-08-07-21-45-33_xxx.gkp.jx3dat` 与 `current.gkp.jx3dat`

`Diag selftest` 仍覆盖真实文件系统的破坏性路径：回收站删文件 / 直接删除 / 回收站删目录 / 越界拦截 / 根目录拦截。

---

## 12. 风险登记册

| 编号 | 风险 | 概率 | 影响 | 应对 |
|---|---|---|---|---|
| R-01 | 玩家名空目录规则随插件版本变化 | 低（本机已验证） | 高 | 多级回退：缓存 → 手动校正 → 未知账号；固定目录白名单可配置 |
| R-02 | 把枫影（`LM`/`lm#data`）误判为懒人插件 | 中 | 高 | 懒人只认 `LR_Plugin` / `LR_Plugin@DATA` 两个精确名；写进单测 |
| R-03 | mp4 缩略图依赖系统解码器，缺 H.265/AV1 时取不到首帧 | 中 | 低 | 失败降级为占位块 + 扩展名，不影响勾选与清理；双击仍交给默认播放器。`DCIM` 路径已于实机确认，路径风险关闭 |
| R-04 | 直接删除不可恢复 | 中 | 极高 | 默认回收站；红色样式 + 二次确认 + 干跑 |
| R-05 | 云团监误删 `local.jx3dat`（本地修改） | 低 | 高 | 保护名单硬编码拦截；只扫 `remote\` 子目录 |
| R-06 | 图片页内存暴涨（大图/海量） | 中 | 中 | 解码降采样 + 虚拟化 + Freeze；>500 张时限制单页渲染 |
| R-07 | `FileSystemWatcher` 抖动导致频繁刷新 | 中 | 低 | 400 ms 防抖 + 只处理增删改事件 |
| R-08 | 目录结构版本变化 | 中 | 高 | 路径全配置化；未知结构只展示不勾选 |
| R-09 | 路径守卫被绕过（junction / `..` / 短名） | 低 | 极高 | 规范化 + 前缀比对 + 拒 reparse point；专项单测 |
| R-10 | 游戏运行中文件占用 | 高 | 中 | 进程检测阻断；占用归入「跳过」而非「失败」 |
| R-11 | 回收站配额不足退化为永久删除 | 中 | 高 | 前置检查驱动器与配额，不满足则警告 |
| R-12 | 用户把筛选误当删除范围 | 中 | 高 | 文案明确「筛选不改变已勾选项」 |
| R-13 | 项目里混入本机绝对路径 | 中 | 中 | 代码评审 + CI 扫描本机盘符路径（仓库内不得出现个人目录） |
| R-14 | 环境自检误判，把能跑的程序拦下来 | 中 | 高 | 发布形态读编译期元数据而非猜路径；只对确定性事实（系统版本 / 位数 / 运行时大版本）判错误；探不到运行时只提示；单测锁死「自包含免检」「探不到不拦」两条 |

---

## 13. 演进

| 版本 | 方向 |
|---|---|
| v1.1 | 缩略图磁盘缓存；图片页按大小排序、批量导出 |
| v1.2 | 清理历史趋势；账号名映射编辑器 |
| v1.3 | 备份代替删除（先打包再删） |
| v2.0 | 结构模板机制，社区提交适配配置 |

---

## 附录 A：配置样例

`config\paths.json`

```json
{
  "version": 2,
  "root": "bin\\zhcn_hd",
  "anchor": "bin64",
  "categories": [
    { "id": "ChatLog",        "path": "interface\\my#data\\{gid}@zhcn[_hd]\\userdata\\chat_log", "glob": "*.db*", "keepLatest": 2, "risk": "Medium" },
    { "id": "FightStat",      "path": "interface\\my#data\\{gid}@zhcn[_hd]\\userdata\\fight_stat", "glob": "*.jx3dat", "risk": "Low" },
    { "id": "LazyPlugin",     "path": "interface\\LR_Plugin",     "requireExists": true, "defaultSelected": true, "risk": "Low" },
    { "id": "LazyPluginData", "path": "interface\\LR_Plugin@DATA","requireExists": true, "defaultSelected": true, "risk": "Low" },
    { "id": "CloudTeamMon",   "path": "interface\\my#data\\!all-users@zhcn_hd\\userdata\\team_mon\\remote", "groupBy": "pair", "risk": "Low" },
    { "id": "LegacyTeamMon",  "path": "interface\\my#data\\!all-users@zhcn_hd\\userdata\\TeamMon", "granularity": "directory", "defaultSelected": true, "risk": "Low" },
    { "id": "Blueprint",      "path": "homelanddir\\Blueprints", "risk": "High" },
    { "id": "Screenshot",     "path": "ScreenShot", "media": ["*.jpg","*.jpeg","*.png","*.bmp","*.gif","*.mp4"], "risk": "High" },
    { "id": "Dcim",           "path": "DCIM",       "media": ["*.jpg","*.jpeg","*.png","*.bmp","*.gif","*.mp4"], "risk": "High" },
    { "id": "RoleData",       "path": "userdata",   "risk": "High" },
    { "id": "HairDye",        "path": "hairdyeingdata", "risk": "Medium" },
    { "id": "ShareStation",   "path": "sharestationdata", "risk": "Low" }
  ]
}
```

`config\regions.json`

```json
{ "version": 1, "valid": ["电信区", "双线区", "无界区"] }
```

`config\settings.json`（v0.8，由设置页写入）

```json
{
  "clientRoot": "D:\\Games\\JX3",
  "verifiedAt": "2026-10-24T21:40:00+08:00",
  "defaultMode": "recycleBin",
  "timeWindow": "all",
  "chatLogKeepCount": 2,
  "fightStatKeepDays": 30,
  "combatLogKeepDays": 30,
  "gkpKeepDays": 90,
  "teamMonKeepGroups": 5
}
```

> `clientRoot` 是**归一化后的客户端根目录**（`…\JX3`），不是 `zhcn_hd`；
> 保留项填 `0` 表示不保留。文件缺失或 `clientRoot` 校验不过 → 启动时直接进设置页。

附录 B：账号目录固定子目录白名单（不视为角色名）

```
audio cache config data export font logs temporary userdata img lang licenses src
```

---

## 14. 实现进展（v0.10）

### 14.1 已完成

| 层 | 内容 |
|---|---|
| 工程 | `JX3Cleaner.slnx`：Core / Diag / App / Tests 四个项目，`global.json` 固定 SDK 10.0.301 |
| 运行时 | .NET 10 + WPF；`App` 构造函数首行设置 `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly` |
| 便携化 | `PortablePaths` 全部指向 `AppContext.BaseDirectory` 下的 `config\` 与 `logs\`，不触碰 `%APPDATA%` / `%LOCALAPPDATA%` / 注册表 |
| 目录定位 | 三种输入归一化（客户端根 / bin / zhcn_hd）+ `bin64` 硬校验 + 9 项清理目录软校验 + 注册表与 5 层深度的扫盘 |
| 设置 | 独立设置页七项各一行；`NumberStepper` 加减框（步长 1 / 5）；首次启动强制先过设置页；保存前二次校验 |
| 扫描 | 14 类数据 / 3 个分组（截图与幻境云图由图片页独立枚举，扫描跳过）；账号名解析、云团监按组（含灰色括号时间）、角色数据按账号整体（过期区服可展开）、错误账号、过期区服、五项可配置保留策略、时间窗口与过滤统计、金团记录正则精筛 |
| 计划 | `PathGuard` 路径守卫 + 父子包含去重 + 干跑清单（在确认弹窗内呈现） |
| 执行 | 回收站（`IFileOperation`，独立 STA 线程）、直接删除（`cmd.exe del`，逐参数传入 `ArgumentList`） |
| 审计 | 每次清理写 `logs\clean-*.json`（v0.8 起不再提供查看入口） |
| 外观 | `Themes/Win11.xaml` 统一圆角控件模板 + DWM 窗口圆角 |
| 界面 | 两个页签（数据清理 / 截图与云图）；三组分区、分类可勾选、分类行可点相对路径；扁平账号树 + 错误账号标红 + 过期区服角标 + 常驻清理按钮 + 进度与结果汇总；时间筛选下方显示过滤提示 |
| 图片页 | Shell `IShellItemImageFactory` 缩略图（图片 + mp4 首帧）、按月分组 + 时间导航、勾选清理（复用 `CleanPlanBuilder`）、行级虚拟化 + 按需解码、`FileSystemWatcher` 增量实时预览、双击调系统默认程序、右键五项 |
| 测试 | xUnit 135 条，覆盖保留策略 / 运行环境自检 / 账号解析 / 分组 / 计划 / 守卫 / 正则 / 命名 / 校验 / 分类目录 / 扫描端到端 |
| 运行环境 | 启动最早期自检（R-10），致命问题弹窗拦截并可从弹窗复制诊断 / 跳转下载；提示只写进设置页「运行环境」行；`Diag env` 输出同一份报告 |
| 发布 | `portable-selfcontained.pubxml`：自包含单文件 61.7 MB，目标机免装 .NET；`RollForward=Major` 让框架依赖版兼容更高大版本运行时 |

### 14.2 本机实测（G: 客户端，v0.9 默认保留策略）

| 指标 | 数值 |
|---|---|
| 账号目录 | 40 |
| 可识别总量 | 19.49 GB / 22,752 文件 |
| 扫描耗时 | 230 ms |
| JCL战斗日志 | 18.66 GB（5 个账号，保留最近 30 天） |
| 聊天记录 | 750 MB（40 个账号，保留最近 2 个） |
| 金团记录 | 769 个可清文件 / 14 个账号 / 4.6 MB（另有 40 个 `current.gkp.jx3dat` 已排除） |
| 云团监 | 75 组 / 65.6 MB（保留最近 5 组） |
| 旧版团监 | 2.5 MB / 22 文件 |
| 角色数据 | 34 个账号 / 11.3 MB（含 2 个错误账号） |
| 错误账号 | 2 个（`13900000000`、`user@qq.com`） |
| 默认勾选可释放 | 687.5 MB |
| 时间过滤（90 天） | 501 项 / 343.7 MB |
| 时间过滤（365 天） | 6,272 项 / 7.22 GB |

### 14.3 自检结果

`dotnet run --project src/JX3Cleaner.Diag -- selftest`：

- 回收站删除文件 ✓
- 直接删除文件 ✓
- 回收站删除目录 ✓
- 越界路径拦截 ✓
- 根目录拦截 ✓

`dotnet test tests/JX3Cleaner.Tests`：135 通过 / 0 失败。

发布产物（v0.9，v0.12 补图标后更新体积）：

- `dotnet publish src/JX3Cleaner.App -p:PublishProfile=portable-selfcontained`
- 自包含单文件 62.1 MB（.NET 10 桌面运行时已打包），框架依赖单文件 724 KB
- 两个 exe 分别跑 `--settings` 截图，设置页「运行环境」显示 `自包含 · 正常` / `框架依赖 · 正常`
  → 元数据随发布配置正确翻转

### 14.4 未完成

- 图片页：按体积排序、跨分类合并浏览、键盘操作（v0.8 明确不做）
- 树节点体积列右对齐（v0.5 已明确放弃，不再排期）
- 图片页行数据仍用 `Clear()` + `Add()` 重建，几千项时会有可感知停顿
- 高 DPI / 多显示器下的列数换算未实机验证（v0.8 明确不做）
- 非托管引导器（用于「.NET 完全缺失」时给中文提示）—— 当前用自包含发布规避，暂不做
