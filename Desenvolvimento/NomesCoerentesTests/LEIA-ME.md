Testes fora do WorldBox. Execute da raiz do repositório com .NET 8:

```sh
dotnet run --project Desenvolvimento/NomesCoerentesTests/NomesCoerentesTests.csproj
```

Compila o Main.cs real e usa Harmony 2.4.2 para interceptar uma API simulada.
Não usa a instalação do jogo nem demonstra execução dentro de Unity/NML real.

A regressão 1.3.0 verifica as quatro raças: nomes completos sem família, herança
por três gerações, vínculos parentais tardios, persistência sem genitores vivos,
estabilidade, nomes personalizados, nomes de famílias e reinos, sucessão normal,
conquista, destinatário aliado, captura cancelada e cidades sem renomeação.
Os testes anteriores de seleção de listas, culturas/religiões/idiomas/clãs,
modelos diretos, filas sem repetição precoce e animais continuam representados.
A morte e a recarga reais do WorldBox ainda precisam ser testadas no jogo.
