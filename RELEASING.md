# Publishing an update to UI Animation Player

| Branch | What it is |
| --- | --- |
| `main` | All internal changes. Users don't see this. |
| `release` | What projects install. It only changes when published. |

**Publishing means copying `main` into `release`.**

## Publishing

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
