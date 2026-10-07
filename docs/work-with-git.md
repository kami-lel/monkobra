# Monkobra: Work with Git

## 队友协作步骤

> [!WARNING]
> 始终使用 `origin`，永远不要碰 `upstream`

### 1. 创建功能分支（branch）

- 先切到 `dev`，点 `Fetch origin`，有更新就 `Pull origin`
- `Current Branch` → `New Branch`，从 `dev` 创建
- 名称不要加 `feature/` 前缀，例如 `fix-cobra-bug`、`add-spider-web`

### 2. （可选）创建自己的开发场景

- 在 `Assets/_Monkobra/Scenes/` 复制主场景 `Lv1Scene.unity`，重命名，例如 `Lv1Scene-cobra-test.unity`
- 只作临时实验用，功能最终必须放回主场景（见第 5 步）

### 3. 在自己的分支工作，常推送（push）

- 只改自己的分支，不碰 `dev` 和 `main`
- 做完一小步就提交（commit），再点 `Push origin`

### 4. 提交前先关闭 Unity

- 💎 关闭 Unity 后再提交，确保所有改动都已写入磁盘

### 5. 完成后合并（merge）`dev` 并解决冲突（conflict）

1. 切到 `dev`，`Fetch origin`，`Pull origin`，更新本地 `dev`
2. 切回自己的分支，菜单 `Branch` → `Merge into current branch`，选 `dev`
3. 出现冲突：先自己解决，解决不了找 Erik
4. 冲突解决后，重新打开 Unity，确认场景正常运行
5. 如果用了自己的开发场景，把功能放进（或复制进）主场景 `Lv1Scene.unity`，不要留在自己的场景里

### 6. 提交并推送

- 关闭 Unity，提交，点 `Push origin`

### 7. 创建拉取请求（Pull Request，PR）

- 点 `Create Pull Request`，目标分支（base）选 `dev`
- 创建后 Erik 负责后续，不用再通知（PM）他

