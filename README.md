[![Build Status](https://travis-ci.org/kmcguire3413/MDACSTest.svg?branch=nightly)](https://travis-ci.org/kmcguire3413/MDACSTest)

The overall testing framework for all the components. The final phase of testing with a complete standard deliverable.

The travis-ci will build each component and also the test component (this repository) which will then execute a series of tests. This is the primary means for testing that covers all components working together instead of individually. 

This project can serve as an example of how the components work together through inspection of the testing framework. By passing `sleepforever` as a command line argument to the test executable the services will stay running and can be accessed via a web browser using the URL http://localhost:34000 .
