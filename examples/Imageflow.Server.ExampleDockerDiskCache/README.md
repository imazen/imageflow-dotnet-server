# Using Imageflow.Server under Docker

## Getting started

1. Copy the examples/Imageflow.Server.ExampleDockerDiskCache folder to your own location.
2. If you rename the .csproj, also rename the .dll reference in the Dockerfile
3. Edit Startup.cs to contain your server configuration.
4. Create an `images` directory and place images in it. These will be accessible at http://localhost:8080/images/*
5. Run `docker build -t imageflow-dotnet-server .` to build the project and tag it
6. Run `docker run -d -p 8080:80 --name ifserver -v "$(pwd)"/imageflow_cache:/app/imageflow_cache -v "$(pwd)"/images:/app/images imageflow-dotnet-server` to run.
7. Open your browser to `http://localhost:8080`
8. To stop the container, run `docker kill ifserver`
9. To delete the container, run  `docker rm ifserver`