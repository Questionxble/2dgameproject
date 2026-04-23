--  Tools And Setup  --
Programming Languages: C#, html
SonarQube version/edition: Latest version (I'm using the cloud service)
Trivy scan type: Local repository
Where the scan was executed: Continuous Integration

--  Scope of Coverage  --
Included parts: the main github branch, or Christian Henken's updates and the rest of the game's scripts.
Excluded parts: multiplayer github branch
Reason: SonarQube finally works with code coverage after my several attempts to configure it went sour over the past few sprints, so now the main branch is actually returning code coverage results, which means I'll need to make improvements to the code coverage statistics (improve/build more unit tests) for main before I expand the code coverage to multiplayer because the line coverage here is 43%, which is inadequate. To give myself slack, I couldn't see any code coverage results the past few sprints for main, so I couldn't detect the issue, and the best course of action next will be to do my best this sprint to increase line coverage in the areas I'm missing.

--  Coverage Trend  --
Not applicable (Just got SonarQube code coverage analysis properly running)
Current main line coverage: 43%

--  Reflection  --
- What was the most problematic area?:
The main branch for sure, since my new-weapon-classes updates PR passed above 80% line coverage. 
- What are you plans to reduce these issues next sprint?
I'm going to build more comprehensive unit testing for the main branch to catch us up by the next sprint demo presentation, as well use code coverage analysis on the multiplayer branch.

--  Evidence --
SonarQube and Trivy report: https://github.com/Questionxble/2dgameproject/blob/new-weapon-classes/Documentation/Sprint5/ChristianHenken/SonarQubeTrivyScans.pdf

--  Statement of Integrity --
This coverage was generated from automated tests executed during this sprint.