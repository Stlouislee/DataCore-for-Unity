---
description: GitHub issue management workflow for bugs and feature requests
applyTo: "**/*"
---

# GitHub Issue Management Workflow

When a bug or feature request is identified in a thread within a Git-initialized project using GitHub as the remote, follow these rules:

## 1. Create an Issue

Use `gh issue create` to log the task. Include:
- A concise, descriptive title
- A detailed description based on the thread context
- Appropriate labels if known (e.g., `bug`, `enhancement`)

Example:
```bash
gh issue create --title "Fix null reference in DataCoreStore" --body "Description of the issue..."
```

## 2. No New Branches

**Do NOT create or switch to a new branch automatically.**

- Perform all work on the current branch unless explicitly instructed otherwise
- Only create a new branch when the user explicitly requests it

## 3. Commit & Link Issues

When asked to commit changes, include the Issue number in the commit message using the `fixes` or `closes` keyword:

```
feat: implement login logic (fixes #123)
```

```
fix: resolve null reference in DataCoreStore (closes #456)
```

This automatically links the commit to the issue and will close the issue when merged (depending on GitHub settings).

## 4. Update Issue After Committing

After committing, use `gh issue comment` to document progress:

```bash
gh issue comment [number] --body "Implemented in commit abc1234. [Summary of changes]"
```

Include:
- The commit hash
- A brief summary of what was done
- Any relevant notes or next steps

## Summary

| Step | Action | Command |
|------|--------|---------|
| 1 | Create issue | `gh issue create --title "..." --body "..."` |
| 2 | Stay on current branch | No action needed |
| 3 | Commit with issue link | `git commit -m "feat: ... (fixes #123)"` |
| 4 | Comment on issue | `gh issue comment 123 --body "..."` |