# Lady Bug enemy package v8

This package builds on v7.

Change in v8:

- `EnemyReleaseBorderTimer` now signals an enemy release at the end of every visible border pass.
- The previous implementation released at the end of the green-fill pass only, then performed a full white-clearing pass without releasing an enemy. In gameplay this looked like one useless border cycle between enemy releases.

Existing v7 behavior is kept:

- enemies are hidden immediately when player death starts after enemy collision;
- after death, enemies and the border timer restart, while consumed collectibles and rotated gate states are preserved.
