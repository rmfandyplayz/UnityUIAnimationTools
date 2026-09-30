# Publishing an update to UI Animation Player

| Branch | What it is |
| --- | --- |
| `main` | All internal changes. Users don't see this. |
| `release` | What projects install. It only changes when published. |

**Publishing means copying `main` into `release`.**

## Before publishing

- **Test it in `GameCraftSep2026Jam`.**
- **Make sure everything is committed and pushed to `main`.** Every new script comes with a `.meta` file, and those have to go up too. Projects that install from GitHub quietly ignore any file whose `.meta` is missing.

## Publishing (GitHub website, recommended)

1. Open this link:
   **https://github.com/rmfandyplayz/UnityUIAnimationTools/compare/release...main**
   - If it says **"There isn't anything to compare"**, `release` is already up to date and there's nothing to publish.
2. Look over the commits and changed files it lists. That's exactly what's about to go out.
3. Click **Create pull request**. Give it a title like `Release 1.0.2`. The version number is in `DOTweenAnimationPlayer/package.json` on `main`. Click **Create pull request** again.
4. Click **Merge pull request**, then **Confirm merge**.
   - The green button must say **Merge pull request**. If it says *Squash and merge* or *Rebase and merge*, click the little arrow beside it and choose **Create a merge commit**. The other two can make the *next* release run into conflicts.

Done. The pull request stays on the repo's **Pull requests → Closed** tab, which doubles as a release history.

## Updating a project

In Unity:

1. **`Window → Package Manager`**.
2. Under **In Project**, select **DOTween UI Animation Player**.
3. Click **Update** at the top right.

The **refresh** button under the package list does *not* check for these updates. It only looks at Unity's own packages. Use **Update**.

**If a project installed it with `#v1.0.0`** at the end of the URL (the first version worked that way): open that project's `Packages/manifest.json`, change `#v1.0.0` to `#release` on the `dotween-ui-animation` line, and save. Update works from then on.

## The version number

`DOTweenAnimationPlayer/package.json` has a `version` line, and it's the number Package Manager shows. It's how you can tell which release a project has. **Claude bumps it whenever it changes the tool, so you don't need to.**

If you change the tool's code yourself, bump it too, before publishing. On the website, open the file, click the pencil icon, change the number and commit. How to pick the new number:

- **1.0.1 → 1.0.2**: bug fixes.
- **1.0.1 → 1.1.0**: something new was added.
- **1.0.1 → 2.0.0**: something that could break animations people already made.
