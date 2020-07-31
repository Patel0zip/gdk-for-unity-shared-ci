FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
WORKDIR /app
COPY ./tools ./tools/
COPY ./ci/docker/entrypoint.sh ./entrypoint.sh
COPY ./scripts/pinned-tools.sh ./pinned-tools.sh

RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get -qq install -y --no-install-recommends gosu && \
    apt-get clean

# Volume to output logs & Buildkite metadata to
VOLUME /var/logs

ENTRYPOINT ["/bin/bash", "entrypoint.sh"]
