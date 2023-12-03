FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

COPY CodeBuddy.sln .
COPY CodeBuddy.WebApp/CodeBuddy.WebApp.csproj ./CodeBuddy.WebApp/CodeBuddy.WebApp.csproj
RUN dotnet restore ./CodeBuddy.WebApp/CodeBuddy.WebApp.csproj

COPY CodeBuddy.WebApp/. ./CodeBuddy.WebApp/
WORKDIR /source/CodeBuddy.WebApp
RUN dotnet publish -c release -o /app

FROM scratch AS installer
ADD https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb /

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN --mount=from=installer,source=/packages-microsoft-prod.deb,target=/packages-microsoft-prod.deb \
   dpkg -i packages-microsoft-prod.deb \
   &&  apt update \
   && apt install -y libmsquic \
   && apt remove -y packages-microsoft-prod \
   && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=DockerDev
ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001
EXPOSE 4433/udp
EXPOSE 4433/tcp
ENTRYPOINT ["dotnet", "CodeBuddy.WebApp.dll"]