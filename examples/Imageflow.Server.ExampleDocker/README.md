# Using Imageflow.Server under Docker

## Getting started

1. Copy the examples/Imageflow.Server.ExampleDocker folder to your own location.
2. If you rename the .csproj, also rename the .dll reference in the Dockerfile
3. Edit Startup.cs to contain your server configuration.
3. Run `docker build -t imageflow-dotnet-server .` to build the project and tag it
4. Run `docker run -d -p 8080:80 --name ifserver imageflow-dotnet-server` to run.
5. Open your browser to `http://localhost:8080`
6. To stop the container, run `docker kill ifserver`
7. To delete the container, run  `docker rm ifserver`