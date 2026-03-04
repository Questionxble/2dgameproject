--  Tools And Setup  --
Programming Languages: C#, html
SonarQube version/edition: Latest version (I'm using the cloud service)
Trivy scan type: Local repository
Where the scan was executed: Continuous Integration

--  Scope of Coverage  --
Included parts: the main github branch, or Christian Henken's updates and the rest of the game's scripts.
Excluded parts: I tried to exclude build html (and code coverage report) files, but perhaps I'm configuring my cloud SonarQube wrong because they still get analyzed in the report, and the html files were the only problematic code in the repository that was detected by SonarQube.
Reason: The previous code coverage files and build html files are not even the main programming of the game, it's the C# files, so they shouldn't ever be considered in code analysis, but I didn't know how to exclude them from SonarQube before I analyzed the repo.

--  Coverage Trend  --
| Sprint   | Line Coverage |
|----------|---------------|
| Sprint 7 |    17%        |

--  Reflection  --
- UI/Canvas methods (CreateScreenUI(), UpdateHealthBar(), etc)
Why: Because testing these would require complex UI prefabs, which is too complex for the method being tested (not worth the cost).
- Animation Controller methods 
Why: Similarly to above, these tests would require animator prefabs and sprite assets which I would need to import externally. Too complex for the worth of the tests.

--  Evidence --
SonarQube and Trivy report: 

--  Statement of Integrity --
This coverage was generated from automated tests executed during this sprint.