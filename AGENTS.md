# Repository Instructions

## Git Sync Workflow Preference

When the user asks to "pull" or "sync" code from another remote branch into the current work:

1. Keep the current local branch checked out unless the user explicitly asks to switch branches.
2. Fetch the target remote branch first.
3. Apply the target branch's code into the current working tree while staying on the current local branch.
4. Commit the synced code on the current local branch.
5. Do not push unless the user explicitly asks to push.

### Current Preferred Pattern

For this repository, if the user asks to pull `origin/master` while working on `win-ui1.0-优化版`, interpret that as:

- stay on `win-ui1.0-优化版`
- fetch `origin/master`
- bring `origin/master` code into the current working tree
- create a new local commit on `win-ui1.0-优化版`
- keep the remote tracking branch as `origin/win-ui1.0-优化版` unless the user explicitly requests a push
