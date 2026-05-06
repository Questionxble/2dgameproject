## Tools And Setup
# Programming Languages: 
C#, html
# SonarQube version/edition: 
Latest version (I'm using the cloud service)
# Trivy scan type: 
local machine
# Where the scan was executed: 
Continuous Integration

## Scope of Coverage
# Included parts: 
the main github branch, or the single player development branch of the game. 
# Excluded parts: 
Multiplayer, since it never PR'ed into the main branch, which is the intention. Multiplayer will eventually be merged witn main after the single player project is relocated to another protected branch.
# Reason: 
I was busy with multiplayer integrations this branch, particularly with replacing the single-instance VM server for our game with a flexible, scalable VM allocator + orchestrator system that handles private lobby sessions w/ dtls encryption for game traffic over UDP (via Unity relay). 

## Coverage Trend
# Sprint 5 Main Branch Line Coverage: 
44%
# Sprint 6 Main Branch Line Coverage: 
34%
# Reason: 
Christian Henken's unit tests in multiplayer aren't being evaluated because they haven't been merged with main yet. The main branch needs to be relocated to a protected single-player branch before this happens. As for the decrease in percentage, that is because Christian Hernandez didn't add any unit tests for his scripts. Proof is in the SonarQubeTrivyReport.pdf file attached. Christian Henken's scripts have varying percentages of coverage depending on which methods he could unit test at the time, but Christian Hernandez's scripts show 0% coverage. Both will be encouraged to do more unit testing on their established code going forward.

## Reflection
# UI/Canvas methods (CreateScreenUI(), UpdateHealthBar(), etc)
Why: Because testing these would require complex UI prefabs, which is too complex for the method being tested (not worth the cost).
# Animation Controller methods 
Why: Similarly to above, these tests would require animator prefabs and sprite assets which I would need to import externally. Too complex for the worth of the tests.
# Plan to reduce issues next sprint: 
Set up a proper CI/CD pipeline instead of manual integration and deployment which invites human error into the development process. Unfortunately, Christian Henken couldn't present most of his integrations this sprint because his VM allocator system was malfunctioning due to a slight misconfiguration in a backend process. The system is robust and elaborate, but there's no point in having it if we can't properly develop with it, so that will be the first issue we address. This CI/CD pipeline will be using github workflows. The next issue after that will be line coverage for the main branch, then relocate the main branch to protected single-player, and then merge multiplayer with main. 

## Evidence
# SonarQube and Trivy report: 
https://github.com/Questionxble/2dgameproject/blob/multiplayer/Documentation/Sprint6/ChristianHenken/SonarQubeTrivyReport.pdf

## Statement of Integrity
This coverage was generated from automated tests executed during this sprint.