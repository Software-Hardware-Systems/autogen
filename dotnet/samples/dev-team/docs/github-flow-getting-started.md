## Prerequisites

Forward leaning preview infrastructure is used in this sample.

- [Chat Completion/Inference/Encoding](#Chat-Completion/Inference-Endpoint-and-API-keys)
- [Github App and Repositories](#how-do-i-setup-the-github-app)
- [Azure Resources](#how-do-I-deploy-the-azure-bits)

### What is this sample about?

This sample demonstrates a proof of concept for integrating AI Agents into a software development team, showcasing how AI can assist in various development tasks. The solution leverages Microsoft Aspire for building distributed applications, Microsoft AutoGen as the AI Agent framework, and Chat Completion/Inference Endpoints for providing intelligence and decision-making capabilities.

The AI Agents in this sample are designed to perform specific roles within a development team, such as project management, development planning, and code implementation. Each agent is responsible for a distinct aspect of the development process, working collaboratively to achieve the team's goals. The GitHub App facilitates seamless integration with GitHub repositories, enabling agents to interact with issues, pull requests, and other repository activities. Azure resources provide the necessary infrastructure for hosting and running the AI Agents, ensuring scalability and reliability.

The intent of this sample is to showcase how AI Agents can be incorporated into a development team to enhance productivity, streamline workflows, and improve overall efficiency. The existing agent population can be expanded or modified to include capabilities tailored to specific needs, making this sample a flexible starting point for various use cases.

By following the provided instructions, users can set up and run the sample locally or within Azure, gaining insights into the potential of AI-driven development teams. This proof of concept serves as a foundation for exploring how AI can be leveraged to support and augment human developers, ultimately leading to more efficient and effective software development processes.

### Chat Completion/Inference Endpoint and API keys

We use [Azure AI Services](https://learn.microsoft.com/en-us/azure/ai-services/) and specifically [Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-services/) to provide the intelligence, decision making, and analysis capabilities of the dev-team members. It provides access to a wide range of Chat Completion, Inference, and Encoding models ranging from small, fast, and inexpensive to large, slow, and expensive. You can pick and choose and experiment to find what is right for you.

- First we need to [create a project in the Azure AI Foundry portal](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/create-projects?tabs=ai-studio)




- - The Chat Completion/Inference endpoint and API keys are used to provide the intelligence, decision making, and analysis capabilities of the dev-team members. Almost any provider will work.
    - [Sign up for an Azure account](https://azure.microsoft.com/en-us/free/)
    - [Create an OpenAI resource](https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAi)
    - Once the resource is created, go to the resource and copy the API key and endpoint. You'll need these values to fill in the `appsettings.json` file.

### How do I setup the Github app?

- [Install the Github app](https://docs.github.com/en/apps/using-github-apps/installing-your-own-github-app)
- [Create labels for the dev team skills](#which-labels-should-i-create)

- [Register a Github app](https://docs.github.com/en/apps/creating-github-apps/registering-a-github-app/registering-a-github-app), with the options listed below:
    - Give your App a name and add a description
    - Homepage URL: Can be anything (Example: repository URL)
    - Add a dummy value for the webhook url, we'll come back to this setting
    - Enter a webhook secret, which you'll need later on when filling in the `WebhookSecret` property in the `appsettings.json` file
    - Setup the following permissions
        - Repository 
            - Contents - read and write
            - Issues - read and write
            - Metadata - read only
            - Pull requests - read and write
    - Subscribe to the following events:
        - Issues
        - Issue comment
    - Allow this app to be installed by any user or organization
    
- After the app is created, generate a private key, we'll use it later for authentication to Github from the app

### Which labels should I create?

In order for us to know which skill and persona we need to talk with, we are using Labels in Github Issues.

The default bunch of skills and personnas are as follow:
- PM.Readme
- Do.It
- DevLead.Plan
- Developer.Implement

Add them to your repository (They are not there by default).

Once you start adding your own skills, just remember to add the corresponding label to your repository.

## How do I run this locally?

Codespaces are preset for this repo. For codespaces there is a 'free' tier for individual accounts. See: https://github.com/pricing
Start by creating a codespace:
https://docs.github.com/en/codespaces/developing-in-a-codespace/creating-a-codespace-for-a-repository

![Alt text](./images/new-codespace.png)

In this sample's folder there are two files called appsettings.azure.template.json and appsettings.local.template.json. If you run this demo locally, use the local template and if you want to run it within Azure use the Azure template. Rename the selected file to appsettings.json and fill out the config values within the file.

### GitHubOptions

For the GitHubOptions section, you'll need to fill in the following values:
- **AppKey (PrivateKey)**: this is a key generated while creating a GitHub App. If you haven't saved it during creation, you'll need to generate a new one. Go to the settings of your GitHub app, scroll down to "Private keys" and click on "Generate a new private key". It will download a .pem file that contains your App Key. Then copy and paste all the **-----BEGIN RSA PRIVATE KEY---- your key -----END RSA PRIVATE KEY-----** content here, in one line.
- **AppId**: This can be found on the same page where you created your app. Go to the settings of your GitHub app and you can see the App ID at the top of the page.
- **InstallationId**: Access to your GitHub app installation and take note of the number (long type) at the end of the URL (which should be in the following format: https://github.com/settings/installations/installation-id).
- **WebhookSecret**: This is a value that you set when you create your app. In the app settings, go to the "Webhooks" section. Here you can find the "Secret" field where you can set your Webhook Secret.

### AzureOptions

The following fields are required and need to be filled in:
- **SubscriptionId**: The id of the subscription you want to work on.
- **Location**
- **ContainerInstancesResourceGroup**: The name of the resource group where container instances will be deployed.
- **FilesAccountName**: Azure Storage Account name.
- **FilesShareName**: The name of the File Share.
- **FilesAccountKey**: The File Account key.
- **SandboxImage**

In the Explorer tab in VS Code, find the Solution explorer, right click on the `gh-flow` project and click Debug -> Start new instance

![Alt text](./images/solution-explorer.png)

We'll need to expose the running application to the GH App webhooks, for example using [DevTunnels](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/overview), but any tool like ngrok can also work.
The following commands will create a persistent tunnel, so we need to only do this once:
```bash
TUNNEL_NAME=_name_your_tunnel_here_
devtunnel user login
devtunnel create -a $TUNNEL_NAME
devtunnel port create -p 5244 $TUNNEL_NAME
```
and once we have the tunnel created we can just start forwarding with the following command:

```bash
devtunnel host $TUNNEL_NAME
```

Copy the local address (it will look something like https://your_tunnel_name.euw.devtunnels.ms) and append `/api/github/webhooks` at the end. Using this value, update the Github App's webhook URL and you are ready to go!

Before you go and have the best of times, there is one last thing left to do [load the WAF into the vector DB](#load-the-waf-into-qdrant)

Also, since this project is relying on Orleans for the Agents implementation, there is a [dashboard](https://github.com/OrleansContrib/OrleansDashboard) available at https://yout_tunnel_name.euw.devtunnels.ms/dashboard, with useful metrics and stats related to the running Agents.

## How do I deploy the azure bits?

This sample is setup to use  [azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/overview) to work with the Azure bits. `azd` is installed in the codespace.

Let's start by logging in to Azure using
```bash
azd auth login
```

After we've logged in, we need to create a new environment provision the azure bits.

```bash
ENVIRONMENT=_name_of_your_env
azd env new $ENVIRONMENT
azd provision -e $ENVIRONMENT
```
After the provisioning is done, you can inspect the outputs with the following command

```bash
azd env get-values -e dev
```
As the last step, we also need to [load the WAF into the vector DB](#load-the-waf-into-qdrant)

### Load the WAF into Qdrant. 

If you are running the app locally, we have [Qdrant](https://qdrant.tech/) setup in the Codespace and if you are running in Azure, Qdrant is deployed to ACA.
The loader is a project in the `samples` folder, called `seed-memory`. We need to fill in the `appsettings.json` (after renaming `appsettings.template.json` in `appsettings.json`) file in the `config` folder with the OpenAI details and the Qdrant endpoint, then just run the loader with `dotnet run` and you are ready to go.



### WIP Local setup

```
dotnet user-secrets set "DevCert:Password" "your_devcert_password"



dotnet user-secrets set "OpenAI:Key" "your_key"

dotnet user-secrets set "OpenAI:Endpoint" "https://your_endpoint.openai.azure.com/"

dotnet user-secrets set "Github:AppId" "gh_app_id"

dotnet user-secrets set "Github:InstallationId" "gh_inst_id"

dotnet user-secrets set "Github:WebhookSecret" "webhook_secret"

dotnet user-secrets set "Github:AppKey" "gh_app_key"
```
