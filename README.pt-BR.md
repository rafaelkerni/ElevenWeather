<div align="center">

# ElevenWeather

### Sem notícias. Sem feeds. Sem distrações. Só o tempo.

Um indicador de clima leve e com cara nativa para a barra de tarefas do Windows 11.

<br>

![ElevenWeather na barra de tarefas](assets/taskbar.png)

<br>

[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11&logoColor=white)](#)
[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](#)
[![Build](https://github.com/rafaelkerni/ElevenWeather/actions/workflows/build.yml/badge.svg)](https://github.com/rafaelkerni/ElevenWeather/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**[English](README.md) | Português | [Español](README.es.md)**

</div>

---

## 🌤️ O que é o ElevenWeather?

O Windows 11 mostra o clima através do painel de **Widgets** — mas esse painel também traz feed de notícias, manchetes e conteúdo que não tem nada a ver com consultar o tempo.

O **ElevenWeather** substitui esse botão único por um indicador exclusivo de clima que fica exatamente no mesmo lugar na barra de tarefas. Sem feed, sem manchetes, sem painéis extras — só a temperatura atual e, a um clique de distância, a previsão horária e diária.

> **Sem notícias. Sem feeds. Sem distrações. Só o tempo.**

---

## ✨ Funcionalidades

- 🌡️ Temperatura e condição atuais, direto na barra de tarefas
- 🖱️ Clique para abrir um cartão de previsão compacto, no estilo do flyout nativo de clima do Windows
- 🕐 Previsão horária, paginada de 5 em 5 itens
- 📅 Previsão diária, com o mesmo layout paginado
- 🫧 Índice de qualidade do ar (US AQI)
- 🖥️ Um indicador por monitor, em um único processo — sem precisar rodar várias cópias
- 📍 Localização detectada automaticamente na primeira execução (por IP), ou definida manualmente buscando uma cidade
- 🌙 Acompanha o tema claro/escuro do Windows automaticamente, ao vivo — sem precisar reiniciar
- 🌐 Interface disponível em **português, inglês e espanhol**, alternável pelo menu
- 🌡️ Celsius ou Fahrenheit, à sua escolha
- 🎨 Escolha do conjunto de ícones de clima — [Meteocons](https://bas.dev/work/meteocons) (padrão), [Fluent Emoji](https://github.com/microsoft/fluentui-emoji) (Flat), ou os ícones nativos do MSN/Bing Weather (copiados do app Bing Weather instalado no seu PC, veja [Ícones de clima](#-ícones-de-clima))
- 🚀 Iniciar com o Windows (opcional)
- ⚡ Leve: sem feed de notícias em segundo plano, sem telemetria, sem anúncios

---

## 🖥️ Prévia

O ElevenWeather ocupa o mesmo lugar onde ficava o widget nativo de clima, alinhado pixel a pixel com ele.

![Indicador do ElevenWeather na barra de tarefas](assets/taskbar.png)

Clique nele para abrir o flyout de previsão, com abas paginadas de previsão horária e diária:

![Flyout de previsão do ElevenWeather](assets/flyout.png)

---

## 💡 Por que o ElevenWeather?

Clima é uma informação de relance: você olha para a barra de tarefas, vê a temperatura, e quando quer mais detalhes, clica. É toda a interação em torno da qual o ElevenWeather foi construído — **olhar de relance → clicar → conferir → voltar ao trabalho** — sem nada disputando sua atenção no meio do caminho.

---

## 🎯 Filosofia de design

**Só o tempo.** O ElevenWeather não tenta virar um painel completo. Já existem ótimos aplicativos para gráficos detalhados, imagens de radar e estatísticas de longo prazo — este não é um deles. O foco é a informação de clima que as pessoas realmente conferem de relance durante o dia.

**Cara nativa.** Um utilitário que fica na barra de tarefas deveria parecer que pertence a ela. O indicador e o flyout do ElevenWeather seguem a própria linguagem visual do Windows 11 (Segoe UI Variable, ícones Fluent, blur acrílico, temas claro/escuro) em vez de parecer um app de terceiros mal encaixado.

**Leve e discreto.** Sem pop-ups pedindo atenção, sem notificações, sem feed de conteúdo. O clima está disponível quando você pede e invisível no resto do tempo.

---

## 🚀 Instalação

Ainda não há instalador ou release publicado — veja [Compilando a partir do código-fonte](#-compilando-a-partir-do-código-fonte) abaixo.

Assim que uma release for publicada, os passos serão:

1. Baixe a versão mais recente na página de Releases do repositório.
2. Extraia os arquivos e execute `ElevenWeather.exe`.
3. Na primeira execução, sua localização é detectada automaticamente; você pode alterá-la a qualquer momento pelo menu do flyout.

O ElevenWeather é voltado para o **Windows 11**.

> **Nota:** o ElevenWeather não é assinado digitalmente. É provável que o SmartScreen do Windows mostre um aviso de "Editor desconhecido" na primeira execução — isso é esperado para um app gratuito e de código aberto sem um certificado de assinatura pago, não é sinal de que algo está errado. Clique em **Mais informações → Executar assim mesmo** para prosseguir. Você pode inspecionar o código-fonte (ou compilá-lo você mesmo) antes de confiar nele.

---

## ⚙️ Configuração

Disponível hoje, pelo menu "•••" no flyout:

- Alterar localização (busca por nome de cidade)
- Iniciar com o Windows
- Idioma da interface (português / inglês / espanhol)
- Unidade de temperatura (Celsius / Fahrenheit)
- Conjunto de ícones de clima (Meteocons / Fluent Emoji / MSN — veja [Ícones de clima](#-ícones-de-clima) abaixo)
- Atualizar o clima agora

Ainda não configurável:

- Intervalo de atualização (fixo em 15 minutos)
- Múltiplas localizações salvas

---

## 🌎 Dados de clima

O ElevenWeather é alimentado pelo **[Open-Meteo](https://open-meteo.com/)** — uma API de clima gratuita e de dados abertos, que não exige chave de API para uso não comercial. Ela fornece condições atuais, previsão horária, previsão diária e dados de qualidade do ar.

A localização é resolvida por:
- Geolocalização por IP (via [ipwho.is](https://ipwho.is/)) na primeira execução, para sugerir uma localização inicial, e
- API de geocodificação do Open-Meteo quando você busca uma cidade manualmente.

O ElevenWeather não gera nem prevê dados de clima por conta própria — a precisão da previsão depende inteiramente das fontes de dados do próprio Open-Meteo.

---

## 🎨 Ícones de clima

Escolha um conjunto de ícones pelo menu "•••" → *Ícones*:

- **[Meteocons](https://bas.dev/work/meteocons)** de Bas Milius (padrão) — licenciado sob MIT, incluído no repositório.
- **[Fluent Emoji](https://github.com/microsoft/fluentui-emoji)** (estilo Flat) da Microsoft — licenciado sob MIT, incluído no repositório.
- **MSN / Bing Weather** — os ícones usados pelo próprio widget de clima do Windows. Eles pertencem à Microsoft e não podem ser redistribuídos, então não estão incluídos no repositório nem nas releases. Em vez disso, na primeira vez que você escolhe esse conjunto no menu, o app os copia localmente do app Clima (Bing Weather) já instalado na sua máquina Windows (nada é baixado). Se esse app não estiver instalado, a opção mostra um aviso e o conjunto atual é mantido.

Texto completo das licenças dos conjuntos incluídos: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## 🔒 Privacidade

O ElevenWeather não tem contas, telemetria ou anúncios. As únicas requisições de rede que ele faz são as necessárias para funcionar:

- Buscar dados de clima e qualidade do ar no Open-Meteo
- Resolver sua localização por IP (uma vez, na primeira execução) ou por busca de cidade
- Nada além disso — sem análise de uso, sem relatório de falhas, sem pings em segundo plano

Sua localização aproximada (latitude/longitude) é enviada ao Open-Meteo para obter a previsão daquele local.

---

## 🧩 Relação com os Widgets do Windows

O ElevenWeather é **independente do sistema de Widgets do Windows** — ele não o desativa, não se conecta a ele nem substitui suas outras funcionalidades (notícias, ações, calendário, etc.).

Ele é voltado para quem mantém os Widgets ativados basicamente por causa do clima na barra de tarefas: desative os Widgets nas Configurações do Windows, e o ElevenWeather preenche essa única lacuna.

---

## 🛠️ Compilando a partir do código-fonte

### Requisitos

- Windows 11 (ou Windows 10 para desenvolvimento)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

### Clonar, compilar e executar

```bash
git clone https://github.com/rafaelkerni/ElevenWeather.git
cd ElevenWeather
dotnet build
```

Execute o executável compilado diretamente:

```bash
src/ElevenWeather/bin/Debug/ElevenWeather.exe
```

...ou rode direto pela CLI:

```bash
dotnet run --project src/ElevenWeather
```

Abrir o `ElevenWeather.sln` no Visual Studio e executar o projeto `ElevenWeather` também funciona.

### Rodando os testes

Os testes unitários (xUnit) cobrem os services, o agendamento de atualizações do `WeatherStore`, os view models e os converters. Chamadas HTTP, timer e armazenamento de configurações são simulados, e os testes de registro usam uma chave descartável, então nada na sua máquina é alterado:

```bash
dotnet test
```

### Compilando uma build de release standalone

Para gerar um único `.exe` autocontido (sem necessidade de instalar o runtime do .NET na máquina de destino):

```bash
dotnet publish src/ElevenWeather -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

O resultado fica em `src/ElevenWeather/bin/Release/win-x64/publish/`.

---

## 📁 Estrutura do projeto

```text
ElevenWeather/
├── src/ElevenWeather/
│   ├── Interop/       # Interop com Win32: rastreamento da barra de tarefas, estilo de janela, acrílico
│   ├── Models/         # Tipos de dados de clima, localização, configurações do app
│   ├── Services/       # Serviços de clima/localização/configurações, autostart, monitor de tema
│   ├── ViewModels/      # View models MVVM da pílula da barra de tarefas e do flyout
│   ├── Views/          # A janela da barra de tarefas e a janela do flyout de previsão
│   └── Resources/      # Dicionários de tema e conjuntos de ícones de clima (Icons/Meteocons, Icons/FluentEmoji)
├── tests/ElevenWeather.Tests/  # Testes unitários (xUnit), sem acesso à rede
└── ElevenWeather.sln
```

---

## 🤝 Contribuindo

Contribuições são bem-vindas — correções de bugs, documentação, traduções ou uma forma melhor de implementar algo.

```bash
git checkout -b feature/minha-melhoria
# faça suas alterações
git commit -m "Add my improvement"
git push origin feature/minha-melhoria
```

Depois abra um Pull Request. Para mudanças maiores, é uma boa ideia abrir uma issue antes para discutir a abordagem.

---

## 🐛 Reportando bugs

Abra uma issue com: o que aconteceu, o que você esperava, passos para reproduzir, sua versão do Windows e uma captura de tela, se relevante.

---

## 📄 Licença

O ElevenWeather é distribuído sob a **Licença MIT** — veja [LICENSE](LICENSE).

---

## Aviso legal

O ElevenWeather é um projeto independente de código aberto. Ele **não é afiliado, endossado, patrocinado ou de qualquer forma associado à Microsoft Corporation**. Microsoft, Windows e Windows 11 são marcas registradas do grupo de empresas Microsoft.

<div align="center">

<br>

**Sem notícias. Sem feeds. Sem distrações. Só o tempo.**

Feito para o Windows 11.

</div>
