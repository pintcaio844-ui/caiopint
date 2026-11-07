# Primeiro teste no WorldBox 0.51.2

Objetivo: verificar a primeira versão no jogo e junto aos mods da sua instalação. Use uma cópia de mundo ou um mundo novo.

## 1. Carregamento e livro

Se o atalho não responder, primeiro confirme que NeoModLoader e Cronista do Mundo aparecem carregados. O Modo experimental ativo sozinho não carrega a pasta de código. A instalação manual do carregador está no [README](../README.md#instalar-a-primeira-versão).

1. Copie a pasta `WorldChronicle` para `WorldBox\Mods` e abra o jogo.
2. Confira **Cronista do Mundo 0.2.0** na lista de mods do NeoModLoader.
3. Carregue o mundo e pressione **Ctrl + Shift + H**.

Esperado: um livro com o nome do mundo e uma página marcada **Em escrita**. Em um mundo que começou no ano 0, a primeira página cobre **0–99**. Se a observação começou mais tarde, o livro começa pelo século correspondente e indica a cobertura parcial.

## 2. Nomes nativos e nomes editados

1. Deixe existir pelo menos dois reinos e algumas cidades.
2. Pelas janelas normais do jogo, renomeie um reino, uma cidade e um rei vivo. Escolha nomes fáceis de reconhecer, como **Reino do Cedro**, **Porto das Estrelas** e **Rainha Aurora**.
3. Consulte **Acontecimentos e nomes deste século**.

Esperado: nomes anteriores e novos aparecem nos registros de mudança. Os acontecimentos seguintes usam os nomes vigentes naquele momento. Um nome editado não cria uma segunda identidade para o mesmo reino.

## 3. História com contexto

1. Provoque uma guerra entre os reinos usando os controles habituais do jogo.
2. Deixe uma cidade ser conquistada. Anote o nome da cidade, os reinos envolvidos e o governante do reino que a recebeu.
3. Se um reino desaparecer durante a guerra, confira seu nome e o ano.
4. Leia o resumo e os acontecimentos da página.

Esperado: o resumo destaca as mudanças mais importantes com os nomes reais. O fim de um reino pode ser situado por sua fundação, guerra, perdas de cidade, sucessão ou renomeação, quando esses fatos foram registrados. O rei daquele momento pode aparecer como governante; o texto não deve presumir que comandou o exército pessoalmente.

Quando surgirem livros ou novos traços culturais, confira também seus nomes. Uma obra escrita deve ter o título e o autor nativos quando fornecidos pelo jogo. A lista de tecnologias específica de outro mod ainda pode precisar de integração própria.

## 4. Virada do século

1. Salve a partida em um slot **A** antes do ano 100, se o seu mundo de teste começou no primeiro século.
2. Acelere a simulação e deixe o calendário passar para o ano **100**.
3. Abra o livro. Volte à página anterior e avance novamente.

Esperado: **Século I — 0–99** está concluído, com resumo e destaque histórico; **Século II — 100–199** está em escrita. A liderança do primeiro século usa os anos observados daquele século. O que acontecer depois não deve reescrever essa página concluída.

Se o mundo começou em outro século, faça o mesmo teste na próxima virada de centena. Saltos de calendário ou simulação que pule anos podem gerar cobertura parcial; o livro não deve inventar observações para esses anos.

## 5. Salvar e carregar sem misturar o futuro

1. Depois da virada, salve em outro slot **B**.
2. Carregue **A**: o livro deve voltar ao estado anterior à virada.
3. Carregue **B**: a página concluída e a página seguinte devem estar presentes.
4. Exporte o livro, use **Copiar pasta** e abra `livro.html` no navegador.

Esperado: o HTML corresponde àquela partida, com índice, uma página por século, nomes e fatos consultáveis. Exportar o livro não substitui salvar o mundo.

## Registrar o resultado

Anote o que funcionou e em qual passo ocorreu um problema. Para erro ao carregar, abrir o livro ou registrar um evento, guarde o trecho de `Player.log` com `[WorldChronicle]` ou com o erro de compilação. O arquivo costuma estar em `%USERPROFILE%\AppData\LocalLow\mkarpenko\WorldBox\Player.log`.

O texto do log permite diferenciar falha do cronista, de uma integração específica ou de outro mod. Não é necessário enviar o save completo para o primeiro diagnóstico.
