<img src="https://github.com/RHEAGROUP/CDP4-SDK-Community-Edition/raw/master/CDP-Community-Edition.png" width="250">

The Concurrent Design Platform Software Development Kit is an C# SDK that that is compliant with ECSS-E-TM-10-25A Annex A and Annex C. The SDK contains multiple libraries that are each packaged as a nuget and avaialble from [nuget.org](https://www.nuget.org/packages?q=cdp4). The SDK is used in the Concurrent Design Platform (CDP4) to create an ECSS-E-TM-10-25A compliant implementation, both for the [Web Services](https://github.com/RHEAGROUP/CDP4-WebServices-Community-Edition), the [Desktop Application](https://github.com/RHEAGROUP/CDP4-IME-Community-Edition) and an experimental [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/client) based [Web Application](https://github.com/RHEAGROUP/CDP4-Blazor). The following libraries are made avaiable in the Community Edition under the [GNU LGPL](https://www.gnu.org/licenses/lgpl-3.0.en.html):

  - CDP4Common 
  - CDP4JsonSerializer
  - CDP4Dal
  - CDP4JsonFileDal
  - CDP4ServicesDal
  - CDP4WspDal

This repository contains example projects that demonstrate how the SDK can be used

## CDP4-SDK-GraphViz

A .NET Core command line application that demonstrates how to access an online CDP4 Server, select an **Engineering Model** and **Iteration** and generate a **dot** file that can be parsed by [GraphViz](https://graphviz.gitlab.io/) to generate a "graphical" representation of an explicit option tree.