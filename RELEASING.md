# Publishing an update to UI Animation Player

A guide for you, the owner of this repo. You publish, and your projects then click **Update**. Everything here works from the GitHub website, so you can do it from any computer, or even your phone.

## How it fits together

The repo has two branches that matter:

| Branch | What it is |
| --- | --- |
| `main` | Where every change lands: Claude's work, your edits. Projects never see it directly. |
| `release` | What projects install. It only changes when you publish. |

**Publishing means copying `main` into `release`.** Until you do, no project gets anything, however many changes pile up on `main`.

`GameCraftSep2026Jam` is the one exception. It's linked straight to this folder on your computer, so it always runs the newest code, published or not. That makes it the place to test before publishing.

## Before you publish

- **Test it in `GameCraftSep2026Jam`.** It's already running the code you're about to publish.
- **Make sure everything is committed and pushed to `main`.** In GitHub Desktop that means no changed files left, and nothing waiting to push. Every new script comes with a `.meta` file, and those have to go up too. Projects that install from GitHub quietly ignore any file whose `.meta` is missing.

## Publishing (GitHub website, recommended)

1. Open this link. Bookmark it, since it's the same every time:
   **https://github.com/rmfandyplayz/UnityUIAnimationTools/compare/release...main**
   - If it says **"There isn't anything to compare"**, `release` is already up to date and there's nothing to publish.
2. Look over the commits and changed files it lists. That's exactly what's about to go out.
3. Click **Create pull request**. Give it a title like `Release 1.0.2`. The version number is in `DOTweenAnimationPlayer/package.json` on `main`. Click **Create pull request** again.
4. Click **Merge pull request**, then **Confirm merge**.
   - The green button must say **Merge pull request**. If it says *Squash and merge* or *Rebase and merge*, click the little arrow beside it and choose **Create a merge commit**. The other two can make the *next* release run into conflicts.

That's it. It's published. The pull request stays on the repo's **Pull requests → Closed** tab, which doubles as a list of every release you've made.

## Publishing with GitHub Desktop (alternative)

It works, but it has one trap: it leaves you on the `release` branch. Anything you commit after that goes to `release` instead of `main`.

1. Make sure everything on `main` is committed and pushed.
2. Click **Current branch** at the top and switch to **`release`**.
3. In the menu bar, **Branch → Merge into current branch…**, pick **`main`**, and click the merge button. Use the plain merge, not *squash* or *rebase*.
4. Click **Push origin**.
5. **Switch back to `main`** (Current branch → `main`). Don't skip this.

## Updating a project

In Unity:

1. **`Window → Package Manager`**.
2. Under **In Project**, select **DOTween UI Animation Player**.
3. Click **Update** at the top right. Unity checks GitHub and installs whatever you last published.

Nothing updates on its own. A project keeps what it has until you click Update, so publishing never changes a project in the middle of your work.

The **refresh** button under the package list does *not* check for these updates. It only looks at Unity's own packages. Use **Update**.

**If a project installed it with `#v1.0.0`** at the end of the URL (the first version worked that way): open that project's `Packages/manifest.json`, change `#v1.0.0` to `#release` on the `dotween-ui-animation` line, and save. Update works from then on.

## The version number

`DOTweenAnimationPlayer/package.json` has a `version` line, and it's the number Package Manager shows. It's how you can tell which release a project has. **Claude bumps it whenever it changes the tool, so you don't need to.**

If you change the tool's code yourself, bump it too, before publishing. On the website, open the file, click the pencil icon, change the number and commit. How to pick the new number:

- **1.0.1 → 1.0.2**: bug fixes.
- **1.0.1 → 1.1.0**: something new was added.
- **1.0.1 → 2.0.0**: something that could break animations people already made.

## If a release breaks something

Don't try to undo it on `release`. Ask Claude to fix it on `main`, then publish again. The fix reaches projects the next time they click Update.
