# Monkobra: Work with Git

## 队友协作步骤

> [!WARNING]
> 请始终使用 `origin`，千万不要碰 `upstream`

### 1. 创建功能分支（branch）

- 先切到 `dev`，点 `Fetch origin`，如果有更新就点 `Pull origin`
- 点 `Current Branch` → `New Branch`，基于 `dev` 创建新分支
- 分支名不要加 `feature/` 前缀，例如 `fix-cobra-bug`、`add-spider-web`

### 2. （可选）创建自己的开发场景

- 在 `Assets/_Monkobra/Scenes/` 下复制主场景 `Lv1Scene.unity`，改个名字，例如 `Lv1Scene-cobra-test.unity`
- 这个场景只用来临时试验，做完后功能一定要放回主场景（见第 5 步）

### 3. 在自己的分支上工作，勤推送（push）

- 只在自己的分支上改动，不要动 `dev` 和 `main`
- 每做完一小步就提交（commit），然后点 `Push origin`

### 4. 提交前先关闭 Unity

- 💎 请先关闭 Unity 再提交，这样所有改动才会确实写入磁盘

### 5. 完成后合并（merge）`dev` 并解决冲突（conflict）

1. 切到 `dev`，依次点 `Fetch origin`、`Pull origin`，把本地 `dev` 更新到最新
2. 切回自己的分支，在菜单选 `Branch` → `Merge into current branch`，然后选择 `dev`
3. 如果出现冲突，请先自己解决
4. 冲突解决后，重新打开 Unity，确认场景能正常运行
5. 如果用了自己的开发场景，要把功能放进（或复制到）主场景 `Lv1Scene.unity`，不要只留在自己的场景里

### 6. 提交并推送

- 关闭 Unity，提交，然后点 `Push origin`

### 7. 创建拉取请求（Pull Request，PR）

- 点 `Create Pull Request`，目标分支（base）选 `dev`
- 创建成功后会自动提示，不需要再通知任何人

> [!NOTE]
> 如果合并 `dev` 时没有冲突，说明你的分支可以直接合并进 `dev`，但仍然要创建 PR，不要跳过
