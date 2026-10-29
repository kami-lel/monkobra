# Monkobra: Work with Git

## 先选情况

| 情况 | 适用场景 | 看哪一节 |
| --- | --- | --- |
| 情况 1：只改主场景 | 功能直接在 `Lv1Scene.unity` 的 prefab 里做 | [情况 1](#情况-1只改主场景) |
| 情况 2：需要开发场景 | 需要自己的测试场景，例如攀爬测试 | [情况 2](#情况-2需要开发场景) |

> [!WARNING]
> 请始终使用 `origin`，千万不要碰 `upstream`

## 通用规则

### 创建功能分支（branch）

- 先切到 `dev`，点 `Fetch origin`，如果有更新就点 `Pull origin`
- 点 `Current Branch` → `New Branch`，基于 `dev` 创建新分支
- 分支名不要加 `feature/` 前缀，例如 `fix-cobra-bug`、`add-spider-web`

### 在自己的分支上工作，勤推送（push）

- 只在自己的分支上改动，不要动 `dev` 和 `main`
- 每做完一小步就提交（commit），然后点 `Push origin`
- 💎 每次提交都要写清楚的提交信息（commit message），说明这一步具体做了什么，不要写 `update`、`fix`、`wip` 这类含糊的话；这些提交信息会被用来生成每位成员的 Weekly Progress
- 一个提交只做一件事，信息用一句话讲明白改了什么、为什么改，例如 `Speed up cobra chase when player idles over 3 seconds`、`Fix spider web not despawning after touching a banana`

### 提交前先关闭 Unity

- 💎 请先关闭 Unity 再提交，这样所有改动才会确实写入磁盘

### 合并（merge）`dev` 到自己的分支

1. 切到 `dev`，依次点 `Fetch origin`、`Pull origin`，把本地 `dev` 更新到最新
2. 切回自己的分支，在菜单选 `Branch` → `Merge into current branch`，然后选择 `dev`
3. 如果出现冲突（conflict），请先自己解决
4. 冲突解决后，重新打开 Unity，确认场景能正常运行

### 谁来合并 PR

- 💎 PR 由 PR 的作者自己合并，不用等别人代劳
- 没有冲突：立刻点 `Merge pull request`
- 有冲突：由你自己负责解决冲突，解决后再合并

## 情况 1：只改主场景

功能直接做在 `Lv1Scene.unity` 的 prefab 里，不需要开发场景。

1. 按[通用规则](#通用规则)创建分支、工作、勤推送
2. 完成后，合并 `dev` 到自己的分支，解决冲突，重新打开 Unity 确认场景能正常运行
3. 关闭 Unity，提交，然后点 `Push origin`
4. 点 `Create Pull Request`，目标分支（base）选 `dev`
5. 没有冲突就立刻合并；有冲突就自己解决后再合并

> [!NOTE]
> 即使合并 `dev` 时没有冲突，也仍然要创建 PR，不要跳过

## 情况 2：需要开发场景

做测试时，可以在自己的分支里创建专属的开发场景（dev scene），避免多人同时改 `Lv1Scene.unity` 而互相冲突。

### 为什么要等 Erik 通知

- 每个人都可以在自己的分支里同时开发，互不影响
- 但同一时间只能有一个分支在合并：开发场景的功能最后都要放进 `Lv1Scene.unity`，两个人同时合并就会在主场景上冲突
- 所以由 Erik 负责协调合并的先后顺序，轮到你时他会通知你

### 开发场景的命名

- 位置：`Assets/_Monkobra/Scenes/`
- 格式：`dev` + 你的名字 + 功能名 + `Scene.unity`，每个词首字母大写
- 例如：`devErikClimbTestScene.unity`、`devBelleSpiderWebScene.unity`
- 复制主场景 `Lv1Scene.unity` 后改名，即可得到起点

### 流程

1. 按[通用规则](#通用规则)创建分支，复制并命名自己的开发场景，在里面做功能和测试，勤推送
2. 功能做完后，创建 PR（点 `Create Pull Request`，目标分支 base 选 `dev`），**先不要合并**
3. 等 Erik 联系你，通知你可以开始合并
4. 合并 `dev` 到自己的分支
5. 解决所有冲突和 bug
6. 让你的功能在主场景 `Lv1Scene.unity` 里正常运行，不要只留在开发场景里
7. 删除自己的开发场景（连同它的 `.meta` 文件）
8. 关闭 Unity，提交，然后再次点 `Push origin`；PR 会自动更新
9. 点 `Merge pull request` 完成合并

### 收尾检查

> [!IMPORTANT]
> PR 合并前，必须同时满足下面两点，缺一不可：
>
> 1. 你的开发场景已经删除
> 2. 你的功能已经能在主场景 `Lv1Scene.unity` 里正常运行
