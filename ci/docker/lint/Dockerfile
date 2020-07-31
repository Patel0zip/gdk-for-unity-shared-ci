FROM mcr.microsoft.com/dotnet/core/sdk:3.1

# Install the linting tool
RUN dotnet tool install --global --version 3.3.111304 dotnet-format
ENV PATH="${PATH}:/root/.dotnet/tools"

# Setup the volume which we will be running from
VOLUME /project
WORKDIR /project

# Entrypoint
ENTRYPOINT [ "dotnet", "format" ]
