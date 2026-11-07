# Livro do Mundo — Cronista do Mundo

Um mod para **WorldBox 0.51.2 no Windows/Steam**, com **NeoModLoader**, que transforma os acontecimentos de cada partida em um livro histórico em português.

**Protótipo 0.4.0, preparado para o primeiro teste dentro do jogo.** O código foi compilado e testado fora do WorldBox. A execução no jogo e a convivência com outros mods ainda precisam de validação.

## Um século por página

O livro acompanha o mundo enquanto ele se desenvolve. Os anos **0–99** formam a primeira página; ao chegar ao ano **100**, esse capítulo é concluído e começa a página dos anos **100–199**. O século atual fica **em escrita**.

Cada página destaca as principais transformações do período: a queda de um reino, suas guerras, cidades perdidas, sucessões, alianças e novas potências. Quando há registros relacionados, o texto situa o acontecimento em sua trajetória anterior. Um registro completo na própria página permite consultar todos os fatos capturados.

Os nomes vêm do jogo: cidades, reinos, personagens, reis, culturas e obras. Edições de nome feitas pelas janelas nativas são registradas com o nome antigo e o novo. A identidade de uma nação permanece ligada ao seu identificador, mesmo depois de renomeada.

A nação de maior projeção é calculada ao longo dos anos observados, com pesos de **50% território, 30% população e 20% exército**. Uma nação que desapareceu continua no retrato histórico do século; empates são apresentados como liderança compartilhada. O livro informa quando a cobertura do período é parcial.

## Instalar a primeira versão

Antes de copiar o cronista, confirme que o **NeoModLoader está instalado e carregando**. Ativar o Modo experimental ou assinar mods na Oficina não confirma, por si só, que o carregador iniciou.

Para instalar manualmente, feche o jogo, baixe [NeoModLoader.dll do projeto oficial](https://github.com/WorldBoxOpenMods/ModLoader/releases/download/latest/NeoModLoader.dll) e coloque o arquivo em `worldbox_Data/StreamingAssets/mods`, dentro da pasta do jogo. Crie essa pasta `mods` se necessário. Depois inicie o WorldBox com o Modo experimental ativo. Consulte também as [instruções oficiais](https://github.com/WorldBoxOpenMods/ModLoader#manual-installation).

São dois locais distintos: o carregador fica em `worldbox_Data/StreamingAssets/mods/NeoModLoader.dll`; o cronista fica em `Mods/WorldChronicle/mod.json` ao lado do executável do jogo.

1. Use **Code → Download ZIP** neste repositório e extraia o arquivo.
2. Na Steam, abra **WorldBox → engrenagem → Gerenciar → Explorar arquivos locais**.
3. Copie **somente a pasta `WorldChronicle`** para a pasta `Mods` do jogo.
4. Confira o caminho: `WorldBox\Mods\WorldChronicle\mod.json`.
5. Abra o jogo com **NeoModLoader** e **Modo experimental** ativos. O carregador compila o código automaticamente.
6. Carregue um mundo de teste e pressione **Ctrl + Shift + H** para abrir o livro.

Se você já instalou uma versão anterior deste mod, substitua sua pasta `WorldChronicle`. O identificador do mod e a chave de histórico no save foram mantidos.

## Ler e salvar

- Use **Século anterior**, **Próximo século** e **Índice** para virar as páginas.
- Abra **Acontecimentos e nomes deste século** para consultar o registro completo daquela página.
- **Salve o mundo normalmente** para preservar o livro dentro da partida.
- **Exportar livro** cria `livro.html`, `livro.md`, a crônica detalhada e `eventos.json`. **Copiar pasta** copia o local dos arquivos.
- O HTML funciona offline, com páginas, índice e navegação pelo teclado.

O livro faz parte do save. Carregar uma partida antiga restaura o histórico daquele ponto. Um mundo novo começa outro livro. As exportações de sessões carregadas são separadas por identificador para preservar versões anteriores.

## O que o cronista observa

Guerras, conquistas, cidades fundadas ou destruídas, rebeliões sinalizadas pelo jogo, reinos criados ou extintos, governantes, alianças e mudanças de nome. Também observa novos traços culturais e livros escritos, usando o nome nativo do conhecimento ou o título e autor da obra.

Ao registrar uma conquista, pode informar quem governava o reino naquele momento. Isso não atribui automaticamente comando militar ao rei. Traços culturais são descritos como desenvolvimento cultural; a escrita de um livro usa a autoria fornecida pelo jogo.

Recursos próprios de **Civilizations**, **WorldBox Frontiers** e **Walls & Sieges**, como tecnologias, eleições ou cercos particulares desses mods, ainda não têm conectores dedicados. Alterações que passam pelos eventos básicos do WorldBox podem ser observadas. A combinação instalada precisa ser testada no jogo.

Uma instalação em um mundo antigo começa a observar naquele momento. Períodos sem dados permanecem identificados. O narrador usa fatos estruturados e não inventa causas, promessas, assassinatos ou termos de tratados. Nomes e acontecimentos anteriores são preservados como foram registrados.

**Não há serviço de IA durante a partida, chave de API ou envio de dados do mundo para serviços externos.**

## Primeiro teste

Siga [o roteiro de teste](docs/TESTE-01.md) em uma cópia de mundo. Ele verifica abertura do livro, nomes editados, conquistas, passagem do século e salvar/carregar.

Veja [o exemplo fictício do livro exportado](docs/livro-exemplo.html) baixando o arquivo e abrindo-o no navegador. As imagens e esse exemplo representam o HTML exportado; o visual dentro do WorldBox ainda depende do teste no jogo.

## Desenvolvimento

O pacote inclui o código-fonte, os testes e [as instruções de compilação](Desenvolvimento/LEIA-ME.md). Os arquivos e bibliotecas proprietários do jogo não são redistribuídos.

Base técnica: [NeoModLoader](https://github.com/WorldBoxOpenMods/ModLoader) e [changelog oficial do WorldBox](https://www.superworldbox.com/changelog).

## Atualização 0.3.0 — livro ilustrado

A janela ganhou textura de pergaminho, capa de couro, filetes dourados e títulos em tinta escura. O livro captura pequenas composições dos símbolos e cores nativos dos três reinos de maior território em cada observação anual. As imagens permanecem associadas ao século e acompanham o save, mesmo se o reino desaparecer. Também aparecem no HTML exportado. São brasões; esta versão não fotografa o mapa nem cenas de batalha. A captura de imagem dentro do Unity ainda precisa de teste nesta atualização; se não estiver disponível, o registro textual continua.

Novas guerras podem ser contextualizadas com alianças rompidas, cidades anteriormente conquistadas ou guerras anteriores entre os mesmos reinos. A ordem dos fatos é preservada; essas relações não são tratadas automaticamente como o motivo da invasão.

Para atualizar, feche o jogo, substitua a pasta WorldChronicle pela nova versão e reabra. Os saves existentes continuam compatíveis. Deixe o calendário avançar um ano para iniciar os registros ilustrados; imagens de séculos anteriores à atualização não são reconstruídas.

## Atualização 0.4.0 — registro nativo e ligações históricas

A captura também acompanha as mensagens adicionadas ao histórico lateral do WorldBox (WorldLogMessageExtensions.add). O texto nativo e os nomes são preservados nos acontecimentos de cada século. A morte de governantes é capturada pelos eventos nativos específicos.

O narrador liga posse, duração aproximada do reinado, morte ou sucessão e guerras do período. Só identifica parentesco quando os identificadores de pais do personagem o confirmam. Conquistas e transferências posteriores da mesma cidade permitem registrar a duração aproximada do domínio. Sem nova transferência registrada, o texto informa apenas o intervalo acompanhado, sem presumir posse ininterrupta não observada.

Não há IA nem necessidade de API. A retrospectiva por século continua, com todos os fatos disponíveis na própria página. A captura do histórico lateral começa após instalar esta atualização; mensagens antigas que não foram registradas não são reconstruídas. O teste de carregamento e captura ao vivo ainda precisa ser feito no WorldBox.
