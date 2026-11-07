# Desenvolvimento

O NeoModLoader compila a pasta WorldChronicle automaticamente dentro do jogo. O mod usa C# 7.3 e .NET Framework 4.8; os testes do núcleo usam .NET 8. As bibliotecas do jogo não são incluídas.

Para executar os testes sem o jogo:

```sh
dotnet run --project Desenvolvimento/CoreTests.csproj
dotnet run --project Desenvolvimento/CenturyTests.csproj
```

Esses projetos restauram Newtonsoft.Json pelo NuGet. Para usar a DLL da sua instalação, passe `-p:NewtonsoftPath="caminho/Newtonsoft.Json.dll"`.

Para compilar o mod externamente, execute o jogo com NeoModLoader uma vez para criar o cache e use:

```powershell
dotnet build Desenvolvimento/Build.csproj -c Release -p:GamePath="C:/Program Files (x86)/Steam/steamapps/common/worldbox"
```

Se necessário, passe `-p:NmlDll="caminho/NeoModLoader.dll"`. Instale o mod pela pasta de código-fonte; não copie as DLLs de referência nem os arquivos de desenvolvimento para dentro dela.

Validação realizada: compilação sem erros ou avisos, 30 testes de persistência/narrativa, 46 testes de séculos/contexto, 334 verificações de metadados e 25 verificações do HTML em Chromium. Os relatórios estão em docs. Esses resultados não substituem testar Unity, o salvamento efetivo e a convivência com outros mods dentro do WorldBox.
