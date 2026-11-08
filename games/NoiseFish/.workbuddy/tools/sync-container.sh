#!/usr/bin/env bash
# 把本地 NoiseFish 的当前分支同步到容器仓库（through-loss-and-gain）的 games/NoiseFish/ 下。
#
# 用法:
#   bash .workbuddy/tools/sync-container.sh              # 同步当前分支
#   bash .workbuddy/tools/sync-container.sh main         # 指定分支
#
# 原理：容器仓库是本地 git 仓库的"聚合层"——
#   本地 NoiseFish（真实开发仓库）→ subtree → 容器仓库 → push 到 GitHub
# 这样每个项目保持独立历史（可单独立仓），同时有一个总览仓库。

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONTAINER_ROOT="$PROJECT_ROOT/.container-build"
REMOTE_NAME="container"
REMOTE_URL="git@github.com:Gjl145/through-loss-and-gain.git"
BRANCH="${1:-$(git -C "$PROJECT_ROOT" rev-parse --abbrev-ref HEAD)}"

cd "$PROJECT_ROOT"

echo "=== 1. 同步前自检 ==="
python .workbuddy/tools/cscheck.py || { echo "自检未通过，中止同步"; exit 1; }

echo
echo "=== 2. 准备容器仓库 ==="
if [ ! -d "$CONTAINER_ROOT/.git" ]; then
  echo "容器仓库不存在，首次创建："
  mkdir -p "$CONTAINER_ROOT/games"
  cd "$CONTAINER_ROOT"
  git init -q -b main
  git config user.name "Gjl145"
  git config user.email "Gjl145@users.noreply.github.com"
  cp "$PROJECT_ROOT/_container_README.md" README.md
  git add README.md
  git commit -q -m "docs: 容器仓库根 README —— 项目索引与提交约定"
fi

cd "$CONTAINER_ROOT"
if ! git remote | grep -q "^$REMOTE_NAME$"; then
  git remote add "$REMOTE_NAME" "$REMOTE_URL"
fi

echo
echo "=== 3. 拉取远端并合并子树 ==="
git fetch "$REMOTE_NAME" main
git checkout -q main
git merge -q --no-edit "$REMOTE_NAME/main" 2>/dev/null || true

echo
echo "=== 4. 把 $BRANCH 作为子树推入 games/NoiseFish ==="
# 先移除旧子树（若有），再重新加入，保证内容与本地一致
if [ -d "games/NoiseFish" ]; then
  git rm -rq --cached games/NoiseFish 2>/dev/null || true
  rm -rf games/NoiseFish
  git commit -q -m "chore: 移除旧的 NoiseFish 子树（准备重新同步）" 2>/dev/null || true
fi

git subtree add --prefix=games/NoiseFish "$PROJECT_ROOT" "$BRANCH" --squash

echo
echo "=== 5. 推送到 GitHub ==="
git push "$REMOTE_NAME" main

echo
echo "✓ 同步完成：分支 $BRANCH → through-loss-and-gain/games/NoiseFish"
