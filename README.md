# PhysBone Motion Preview

**在 Unity 编辑器里预览 VRChat PhysBone 的摆动、惯性和链式联动，并直接编辑完整的 `VRCPhysBone` 参数，减少手动进出 Play Mode 和反复晃动角色的次数。**

Unity 2022.3 · VRChat Avatars SDK · Editor-only · 中文 / English / 日本語 / 한국어 · v0.19.0

> Preview VRChat PhysBone motion and edit the full `VRCPhysBone` settings directly in the Unity editor, without repeatedly entering Play Mode and shaking the avatar by hand.

---

## English Quick Start

1. Open `Tools > Avatar Chamber > PhysBone 动态预览`（菜单标签固定为中文）.
2. Select any part of your avatar in the Hierarchy.
3. Choose a target PhysBone, adjust the parameters, or click a preset.
4. To change PhysBone parameters in Play Mode, edit them in the **PhysBone** tab, then use **Save Changes** or **Save & Exit**.
5. Switch the UI language in **Settings > Language**: 中文, English, 日本語, 한국어.
6. Optional: block VRCFury or NDMF / Modular Avatar from processing the avatar in Play Mode.

---

## 功能

- 选中角色任意部位（骨骼、网格、Avatar 根节点）即可显示通用操作面板
- 「目标动骨」下拉框在角色的所有 `VRCPhysBone` 之间切换
- 7 种预设动作：上下摆动、左右摇摆、前后晃动、画圈、高频抖动、风吹、脉冲回弹
- 可调幅度、频率、时长；时长为 `0` 表示循环播放
- Play Mode 中可直接编辑 PhysBone 参数，退出后按需写回场景
- 「动骨」标签内置完整版 VRC PhysBone 设置
- Scene 视图显示所有 PhysBone 骨骼链；当前目标为黄色，其余为青色
- `X` 快捷键把 Scene 视图中心移到鼠标下方
- 构建阻止：阻止 VRCFury、MA / NDMF 在 Play Mode 处理 Avatar
- 界面支持中文 / English / 日本語 / 한국어
- 不依赖 VRChat SDK 程序集：未安装 SDK 时仍可编译，窗口会给出提示
- 只包含 Editor 代码，不向角色添加运行时脚本

## 安装

### 方式一：本地包

把本仓库文件夹放到目标项目的 `Packages/` 目录下即可，Unity 会自动识别为嵌入式包；文件夹名可以不同，包名以 `package.json` 为准。

### 方式二：Git URL

1. 打开本仓库页面，点击 `Code` → `HTTPS` 复制仓库地址
2. Unity → Window → Package Manager → `+` → Add package from git URL
3. 粘贴仓库地址

> 公开仓库可直接使用 HTTPS；私有仓库需要配置对应的 Git 凭据。

### 环境要求

- Unity 2022.3
- VRChat Avatars SDK（需要 `VRCPhysBone`）
- VRCFury / Modular Avatar 为可选项，仅用于构建阻止功能

## 快速开始

1. 打开菜单 `Tools/Avatar Chamber/PhysBone 动态预览`
2. 在 Hierarchy 中选中角色的任意部位（骨骼、网格、Avatar 根节点都可以）
3. 在「预览」标签用「目标动骨」下拉框选择要预览的 PhysBone
4. 调整幅度、频率、时长，点击任意预设动作；时长为 `0` 表示循环播放
5. 需要改 PhysBone 参数时切到「动骨」标签
6. 需要切换界面语言、修改驱动方式、目标动骨或构建阻止时切到「设置」标签
7. 第一次点击动作会自动进入 Play Mode；停止按钮或退出 Play Mode 后 Transform 自动复原
8. Play Mode 中改过的 PhysBone 参数，用顶部「保存更改」或「保存并退出」写回场景；「退出预览」会放弃这些修改

## 界面

窗口顶部有「预览 / 动骨 / 设置」三个标签，默认打开「预览」：

- **预览**：物理预览控制栏、动骨参数、动作参数、预设动作、播放状态
- **动骨**：完整版 VRC PhysBone 设置，所有字段可直接编辑
- **设置**：界面语言、构建阻止、目标动骨、驱动对象

## 语言 / Language

「设置」标签最上方提供界面语言切换：

| 语言 | 名称 |
| --- | --- |
| 中文 | 中文 |
| English | English |
| 日本語 | 日本語 |
| 한국어 | 한국어 |

切换后立即生效，并保存在本机 `EditorPrefs` 的 `AvatarChamber.PhysBoneMotionPreview.Language` 中。主窗口标题、标签、按钮、提示、完整 PhysBone 设置和悬停说明都会跟随语言；语言名称本身保持各自的母语写法。

> Unity 菜单路径 `Tools/Avatar Chamber/PhysBone 动态预览` 是 `[MenuItem]` 的编译期常量，不能随语言动态切换；窗口标题和窗口内内容会正常切换。

## 物理预览控制栏

位于「预览」标签最上方：

- **物理预览**：显示当前是否处于预览 / Play Mode，以及是否可以保存更改
- **显示骨骼**：在 Scene 视图绘制角色所有 PhysBone 的骨骼链；当前目标为黄色，其余为青色
- **重新居中 Gizmo**：把 Scene 视图中心移到目标 PhysBone 的根骨骼
- **在鼠标下方放置 Gizmo**：把视图中心放到鼠标所指的位置
- **设置放置快捷键...**：打开 Unity 快捷键设置；默认 `X` 在 Scene 视图把中心移到鼠标下方
- **保存更改**：把 Play Mode 中的参数修改暂存，退出 Play Mode 后写回场景
- **保存并退出**：保存并立即退出 Play Mode
- **退出预览**：放弃本次参数修改并退出 Play Mode
- **使用 Inspector 修改物理组件**：打开 VRC PhysBone 原面板

## 构建阻止

位于「设置」标签中有两个开关：

- 阻止 VRCFury 在 Play Mode 构建
- 阻止 MA / NDMF 在 Play Mode 构建

说明：

- 不依赖 Hierarchy 选中对象，窗口打开即可操作
- 默认启用；勾选后立即生效并持续保持，不再局限于某次预览
- 取消勾选会重新启用对应工具的 Play Mode 处理
- 只影响 Play Mode 预处理，不会阻止上传或正式构建

## 目标动骨（设置标签）

「设置」标签中的「目标动骨」显示当前目标，并可直接修改：

- **PhysBone**：拖入场景中的 `VRCPhysBone`，或点击对象选择器切换
- **层级路径**：根据当前 PhysBone 自动生成，只读，可选中复制
- **根骨骼**：这条 PhysBone 链条的起点，可直接修改；修改会写入 `rootTransform`，可用 `Ctrl+Z` 撤销
- **链长（估算）**：只统计当前 PhysBone 控制的链条；遇到子级上的下一个 PhysBone 会停止，这是 VRChat 的链条边界

## 动骨（完整版设置）

「动骨」标签包含完整版 VRC PhysBone 设置，选中骨骼后直接显示。所有字段按 VRC 原面板分组：

- **骨骼与变换**：根骨骼、忽略的骨骼、末端位置、多子骨骼模式
- **受力**：集成方式、`pull` / `spring` / `stiffness` / `gravity` / `gravity falloff` / `immobile` 及各自曲线、固定方式
- **碰撞**：允许碰撞、碰撞权限、碰撞半径及曲线、碰撞体列表
- **拉伸与压缩**：最大拉伸、最大压缩、拉伸运动及各自曲线
- **限位**：限位方式、最大 X / Z 角度及曲线、旋转限位及曲线、静止冻结轴
- **抓取与姿势**：允许抓取 / 摆姿势、抓取权限、姿势权限、吸附到手、抓取移动
- **选项**：根骨骼被动画驱动、禁用时重置、控制参数名、显示 Gizmo、骨骼 / 限位透明度、忽略其他 PhysBone
- **版本**：显示当前 PhysBone 配置版本

说明：

- 使用 `SerializedProperty` 绘制，列表、曲线、引用、权限过滤都可以编辑
- 编辑会记录到 Unity 的撤销系统（`Ctrl+Z`）
- 运行模式下也可以编辑；退出 Play Mode 前用顶部「保存更改」或「保存并退出」写回场景
- 写回只覆盖配置字段；根骨骼、忽略骨骼、碰撞体列表等对象引用和列表不会保存
- `isAnimated` 不参与写回（从动骨根节点预览时会被临时开启），请在编辑模式修改
- 如果 FX 层正在动画驱动 PhysBone 参数，请先关闭对应动画再保存，避免把动画值写回场景

## 参数说明

「预览」标签会以「参数名 + 滑块 + 数值」的形式显示以下参数，可直接拖动修改，鼠标悬停查看解释：

| 参数 | 中文 | 效果 |
| --- | --- | --- |
| `pull` | 回拉力 | 越大，骨骼越快回到静止姿态 |
| `spring` | 弹性 | 越大，摆动越有弹性，回弹越明显 |
| `stiffness` | 硬度 | 越大，骨骼越硬，越不容易弯曲 |
| `gravity` | 重力 | 越大，骨骼越容易被向下拉 |
| `immobile` | 固定度 | 越大，骨骼越不容易跟随整体移动 |

「构建阻止」「目标动骨」「驱动对象」都是可折叠展开栏，点击标题即可收起或展开。

## 驱动对象

位于「设置」标签：

- **整个模型（Avatar 根节点）**：默认，推荐。PhysBone 作为被动跟随者解算，限位和惯性表现与手动移动主物体一致
- **动骨根节点**：直接驱动骨骼本身，插件会临时把 `isAnimated` 设为 `true` 并重建组件，可能出现不自然的抽搐
- **父级骨骼**：适合头发、耳朵这类挂在头部的链条
- **自定义 Transform**：驱动指定对象

支持位移（米）和旋转（度）两种驱动方式。

## 预设动作

| 动作 | 运动方式 | 适合验证 |
| --- | --- | --- |
| 上下摆动 | 正弦位移/绕 X 旋转 | 重力、pull |
| 左右摇摆 | 正弦位移/绕 Y 旋转 | 侧向弹簧 |
| 前后晃动 | 正弦位移/绕 Z 旋转 | 前后惯性 |
| 画圈 | X/Z 相位差 90° | 多轴联动 |
| 高频抖动 | 多频叠加小幅度 | 阻尼是否合适 |
| 风吹 | 双正弦叠加 | 自然感 |
| 脉冲回弹 | 梯形脉冲 | spring 回弹速度 |

## 注意事项

- 只支持场景中的 PhysBone，不支持直接预览 Prefab 资产
- 动作幅度单位取决于「运动方式」：位移是米，旋转是度
- 预设动作只改 Play Mode 内的 Transform；PhysBone 参数只有在点击「保存更改」或「保存并退出」后才会写回场景
- 建议保持 Unity 窗口在前台；Unity 在后台时 Play Mode 可能暂停，预览会跟着停住
- Gesture Manager / AV3 Emulator 同时驱动同一骨骼时可能互相覆盖，建议关闭它们的自动动作后再对比
- 进入 Play Mode 会触发域重载，首次进入可能需要等待几秒

## License

MIT License. See [`LICENSE`](LICENSE).

## 卸载

从目标项目 `Packages/` 中删除本文件夹即可，不会留下其他资产或设置。

---

> 本项目不是 VRChat Inc. 官方产品。VRChat、PhysBone 及相关名称为其各自权利人的商标或标识。
