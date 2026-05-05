1. Tools Used:
Programming Languages: C#
SonarQube version/edition:
Trivy scan type: filesystem
The trivy scan was executed locally, the SonarQube scan was executed

2. Required Metrics:
(see TrivySonarQubeReports pdf)

3. Scope: 
The new-weapon-classes branch was scanned for both tools, since that is where the majority of this sprint's scripting resides.
The clean environment updates that Christian Henken have been producing includes minor environmental interactables in script, but those are barely any lines of code, so I didn't include them in this sprint's analysis.
Wolfie's contributions still haven't been pushed to the new repo.

4. Trend:
--
5. Reflection
The most problematic area this sprint was definitely the support class (Christian Henken's contributions this sprint). The live demo could  barely be presented due to performance issues from either Christian's new Windows laptop or the support class integration, possibly via the increased use of animators and animating parts in the support class logic. Maybe this next sprint, we can discuss optimization and other ways to improve gameplay performance before next sprint. It's critical that we cannot allow the FPS to tank. 
Christian Henken will debloat his computer of processes taking up system resources, and then we'll see if performance improves. After that, if the problem persists, animation optimization is next.

“This static analysis was generated using automated tools during this sprint.”