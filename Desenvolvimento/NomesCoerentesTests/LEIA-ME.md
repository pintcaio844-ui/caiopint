Testes fora do WorldBox. Execute da raiz do repositório com .NET 8:

```sh
dotnet run --project Desenvolvimento/NomesCoerentesTests/NomesCoerentesTests.csproj
```

A API de jogo e BasicMod são simulados; o mod é compilado a partir do seu Main.cs real. Harmony 2.4.2 é usado para testar interceptação neste runtime .NET 8. A versão de Harmony embarcada pelo NML pode ser diferente. Estes testes não demonstram carregamento dentro do Unity/WorldBox.

Verifica listas não vazias e sem duplicatas internas, seleção por raça e grupo, sexo, criatura desconhecida, prefixos de assets, caso sem ator, esgotamento e renovação das listas, assinatura do gerador, interceptação Harmony e caminhos adicionais de alianças, mundos, guerras, livros, exércitos e linhagens.
