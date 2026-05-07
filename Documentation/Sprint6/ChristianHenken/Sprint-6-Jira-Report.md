Sprint 6

| Metric                           | Count |
|----------------------------------|-------|
| Issues committed at sprint start |   29  |
| Issues completed                 |   12  |
| Issues not completed             |   17  |
| Issues added mid-sprint          |   0   |

Issue Breakdown By Type:
|  Type    |  To Do  |  In Progress  |   Done   |
|----------|---------|---------------|----------|
|  Story   |    1    |       2       |    0     |
|  Task    |    2    |       1       |    1     |
|  Bug     |    3    |       0       |    4     |
|  Feature |    5    |       2       |    8     |

Per-Student Work Allocation:
|  Student.    |  Issues Assigned  |  Issues Completed  |
|--------------|-------------------|--------------------|
| Chris Henk   |         11        |         8          |
| Chris Hern   |         6         |         1          |

Estimation and Accuracy:
| Metric                           | Value |
|----------------------------------|-------|
| Total story points committed     |   19  |
| Total story points completed     |   11  |
| Completion %                     | 57.9% |

Workflow Discipline:
Issue: Add animation for valor shard's right click attack
To Do -> In Progress
(This is a relatively simple fix, since I just need to add two special animations for the charge and attack release and call those animations in the scripts)
In Progress -> Done
(Low amount of testing needed. If a noticeable bug arises, a new issue will be made)

Issue: Add a blocking feature
To Do -> In Progress
(This was tricky because I needed to add a new value inside of every existing enemy attack in the game which classifies the attack as blockable/unblockable, and I also needed to consider race conditions with blocks. The least of the worries was setting up the block mechanism on the player (A shield sprite appears over the player, nothing fancy))
In Progress -> Done
(If noticeable bugs arise, new issues will be made)

Issue: Add health item/buff collectables
To Do -> In Progress
(This required programming a unique collectable item script which transforms a gameobject it's attached to an either auto-collecting or key-pressed collecting item. The added items are health (auto collect, heals player), buff (auto collect, buffs player depending on the buff type attached), silver pennies (the currency that will be used for the game/NPCs), and permanent relics (The lockpick for unlocking locked chests, and the bottled soul for providing the player a tutorial experience with window panel guides.))
In Progress -> Done
(If noticeable bugs arise, new issues will be made)

Issue: Fix some multiplayer desyncing (Some client actions are local only)
To Do -> In Progress
(This was just simply due to inadequate ServerRPC calls where I needed them (In originally monobehavior/single-player methods). After skimming the weapon, playermovement, and other player scripts, I was able to patch the remaining bits of localized functionality)
In Progress -> Done

Issue: Fix some of the client deployments not connecting to the VM server
To Do -> In Progress
(This consumed most of the time this sprint, since I decided to switch the single-instance VM server setup (AWS) we had for online multiplayer for a scalable, flexible, and cost-efficient VM allocator/orchestrator system in GCP. Instead of one server being the only session that be connected to and played, which is not a release-ready game model, now anybody who downloads a client of this game and attempts to play it will have access to private game sessions that they can play in with their friends.)
In Progress -> Done
(Currently fixed as of after the 5/5/2026 demo.)

Issue: Synchronize enemy behavior across the server
To Do -> In Progress
(This was completed in the same workflow as the "Fix some multiplayer desyncing," since I just needed to add ServerRPC calls in the EnemyBehavior script lines where actions were still localized (Or not ClientRPCing to the clients))
In Progress -> Done
(If noticeable bugs arise, new issues will be made)

Issue: Add a lobby login with customizable security settings
To Do -> In Progress
(This was completed also in the same workflow as "Fix some of the client deployments not connecting to the VM server." It's now required to create a lobby to play the game, since the single-instance VM model was exchanged for this VM allocator/orchestrator. There are only bugs when the manual integration and deployment is done wrong (misconfiguration), hence the lackluster demo on 5/5/2026. The next priority I need to cover after this sprint is establishing a CI/CD pipeline so that this human error never occurs again)
In Progress -> Done

Issue: Connect a game progress save database to the game.
(This implementation utilizes a firestore database on each allocated VM per lobby to keep a temporary save file for the game session. The creator of the lobby owns this save data and will be the only one who can export/reimport it. The player can only upload the save data in the startup menu screen before any lobby is created. Once the game session is quit by all players and the VM auto shuts off, the save data will be lost forever, so I made sure to add a windowed warning informing the player before they quit.)
In Progress -> Done
(If noticeable bugs arise, new issues will be made)

Issue: Add NPC's that give out quests in return for gold/health/buffs/items.
(Reprogrammed the attackdummy script for the NPCs to make them attack less, wander more, and be able to talk. The NPCs have two methods of chatting, chat bubbles (which are triggered by random player actions at random. Their responses are stored in a local json file.), and the bottom-screen rectangle dialogue box (Shows the NPCs icon + their dialogue. Will be used for quests, in comparison to the chat bubbles). Specifically with the rectangle dialogue box feature, after a specific dialogue node is visited, the NPC can choose to give the player silver pennies, health, buffs, or recently added items).
In Progress -> Done
(If noticeable bugs arise, new issues will be made)

Blockers and Scope Changes:
- Christian Hernandez still doesn't have a ready-to-use starting map of our game yet because his map file got corrupted over the last sprint, so he had to start over. I was anticipating about moving my contributions onto his map, but that can wait for a later sprint, I suppose.
- Nothing about the scope changed, my updates were successful, we're going to continue progress with our original development plans.
- Almost everything that was added from the previous sprint onto this sprint has been carried to the next one due to me dedicating this sprint to reworking the weapon attack logic and planning out enemy types, storm shard and whisper shard ultimate abilities, and general weapon class responsibilities in battle. A lot of time was spent configuring the individual animation controllers (8 of them so far) to manage the animation processing on the enemies, including configuring enemy behavior parameters in the behavioral scripts on their gameobjects. Next sprint, I plan to complete a lot more issues since I spent this last sprint preparing the scripting/animations for this upcoming one.

Jira Evidence Links:
Backlog: https://soulful-journey.atlassian.net/jira/software/projects/SJOURNEY/boards/1/backlog
Board: https://soulful-journey.atlassian.net/jira/software/projects/SJOURNEY/boards/1?jql=Sprint%20%3D%20134&sprints=134