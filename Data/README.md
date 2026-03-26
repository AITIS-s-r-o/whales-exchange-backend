# Entity Framework Database Contexts

## Creating Database Context From Scratch

* If the tool is not installed, run `dotnet tool install --global dotnet-ef`.
* Change the code of the context as you need.
* Delete `Migrations` folder.
* Execute the following commands from the root folder (e.g. using `Tools > NuGet Package Manager > Package Manager Console` in your Visual Studio):
    ```
    dotnet ef migrations add InitialCreate
    dotnet ef database update
    ```

## Resources

* https://docs.microsoft.com/en-us/ef/core/get-started/overview/first-app?tabs=netcore-cli