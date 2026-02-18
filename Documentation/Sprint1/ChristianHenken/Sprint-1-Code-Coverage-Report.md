--  Tools And Setup  --
Programming Languages: C#, html
Test Frameworks: 
Coverage Tools Used: Unity Code Coverage Package (built-in)

--  Coverage Metrics  --
EditMode Tests:
| Metric                   | Percentage |
|--------------------------|------------|
| Line coverage            |    17.1%   |
| Branch coverage          |    0%      |
| Function/Method coverage |    23.7%   |
| Statement coverage       |    17.1%   |

PlayMode Tests:
| Metric                   | Percentage |
|--------------------------|------------|
| Line coverage            |    17%     |
| Branch coverage          |    0%      |
| Function/Method coverage |    26.2%   |
| Statement coverage       |    17%     |


--  Scope of Coverage  --
Included parts: the main github branch, or Christian Henken's updates and the rest of the game's scripts.
Excluded parts: the starting map branch (which will PR into main), or Christian Hernandez's updates.
Reason: Christian Hernandez's map involved minimal scripting, only a sword activation trigger which unanchored the chandelier and had it fall into the floor. The vast majority of the game's scripting is in the main branch. Christian Hernandez's code will be included in the test coverage score in the upcomimg sprint.

--  Coverage Trend  --
| Sprint   | Line Coverage |
|----------|---------------|
| Sprint 7 |    17%        |
| Sprint 1 |    10%        |

--  Weak Areas  --
- UI/Canvas methods (CreateScreenUI(), UpdateHealthBar(), etc)
Why: Because testing these would require complex UI prefabs, which is too complex for the method being tested (not worth the cost).
- Animation Controller methods 
Why: Similarly to above, these tests would require animator prefabs and sprite assets which I would need to import externally. Too complex for the worth of the tests.

--  Evidence --
Edit mode tests report: https://github.com/Questionxble/2dgameproject/blob/main/Documentation/Sprint1/ChristianHenken/EditModeTests.html
Play mode tests report: https://github.com/Questionxble/2dgameproject/blob/main/Documentation/Sprint1/ChristianHenken/PlayModeTests.html

--  Statement of Integrity --
This coverage was generated from automated tests executed during this sprint.