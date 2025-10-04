FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Interstellar.sln ./
COPY Interstellar.Server/Interstellar.Server.csproj Interstellar.Server/
COPY Interstellar.Messages/Interstellar.Messages.csproj Interstellar.Messages/

RUN dotnet restore Interstellar.Server/Interstellar.Server.csproj

COPY . .
RUN dotnet publish Interstellar.Server/Interstellar.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 8000

ENTRYPOINT ["dotnet", "Interstellar.Server.dll"]
CMD ["0.0.0.0:8000"]
