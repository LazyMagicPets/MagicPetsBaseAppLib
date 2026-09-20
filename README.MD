# BaseAppLib 
This solution generates the following packages for use throughout the EventIt family of applications:

- BaseApp.BlazorUI - contributes to the namespace BlazorUI
- BaseApp.ViewModels - contributes to the namespace ViewModels

To facilitate the development and integrtion testing of these libraries, we have the following projects:
- BlazorUI - contributes to the namespace BlazorUI but is not provided as a package. Is used only in the WASMApp project.
- ViewModels - contributes to the namespace ViewModels but is not provided as a package. Is used only in the WASMApp project.
- WASMApp - A WebAssembly application that uses the BlazorUI and ViewModels projects for development and testing purposes.

## Development Process 
Components are developed in the BlazorUI project and ViewModels projects and when ready, they are moved to the BaseApp.BlazorUI and BaseApp.ViewModels projects respectively.

## Rationale 
The BaseApp.BlazorUI and BaseApp.ViewModels projects are designed to be used across multiple applications within the EventIt family. By separating these components into their own packages, we can ensure that they are reusable and maintainable, while also allowing for independent development and testing. In addition, when debugging a WASM, it is difficult to debug into a package. Since all of the components are in the same solution, it is easy to debug them.

## BaseApp.BlazorUI 
This package contains those components used by all of the EventIt family of applications.

## BaseApp.ViewModels
This package contains those view models used by all of the EventIt family of applications.

## Notes
When creting a new EventIt application, you can start by copying the WASMApp, ViewModel and BlazorUI projects.

