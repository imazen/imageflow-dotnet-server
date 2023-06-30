# This project is used for i.zr.io, the live imageflow demo server

## Getting started

1. Run `docker build -t imageflow-dotnet-server .` to build the project and tag it
2. Run `docker run -d -p 8080:80 --name ifserver imageflow-dotnet-server` to run.
3. Open your browser to `http://localhost:8080`
4. To stop the container, run `docker kill ifserver`
5. To delete the container, run  `docker rm ifserver`