[![Build Status](https://travis-ci.org/kmcguire3413/MDACSTest.svg?branch=nightly)](https://travis-ci.org/kmcguire3413/MDACSTest)

The overall testing framework for all the components. The final phase of testing with a complete standard deliverable.

If you wish to hack on the source then this would be the optimal project to clone and start with. You can work with the submodule
projects; however, since some are shared across different services you have to be mindful of changing some core structures and
functionality such as that within MDACSAPI or anything that affects it. A change in one of the microservices will not be
reciprocal unless you ensure the other projects have that specific change.

For microservices, one of the tenets is to remove hard code dependencies between each service. Unfourtunately, to prevent rewrite of 
significant common code there are two assemblies that are shared. These are MDACSAPI and MDACSHTTPServer. However, this is a simple soft
sharing and each project maintains its own reference to a specific commit of the assembly which it uses internally. The microserver
projects only share data over their network protocol. None share data in memory; however, this project (MDACSTest) is an exception and
not the rule. In this case, each service is spawned as a thread, thus, they all do share the same memory yet the code of each service
makes no reference to the other service and assumes that it is running in its own process potentially on a different physical machine
than the other services.

Therefore, each project will have its own MDACSAPI submodule at a specific commit. It is likely all will have the same commit, but it is
not required. Which allows each project to upgrade its own dependencies and perform its own testing with its own team or timeline. This
project is a final testing phase which takes all the services and tests them as a whole. Currently, this project is the only substantial
set of unit tests; however, over time each project can build its own set of tests that a specific to its deliverable at that time.

If you build within the `/MDACSTest/MDACSTest` directory with `dotnet run` then the path to the web resources needs to be specified. For
example, `dotnet run "../MDACSApp/MDACSApp/webres"`. You can also pass an additional argument like `dotnet run "../MDACSApp/MDACSApp/webres" sleepforever`
which causes the process to wait after running the tests. This allows one to access the web interface, perform other non-automated tests, or
use external tools to demo or test the system.
