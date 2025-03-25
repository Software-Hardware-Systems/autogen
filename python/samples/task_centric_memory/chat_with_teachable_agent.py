import os
from autogen_agentchat.agents import AssistantAgent
from autogen_agentchat.ui import Console
from autogen_ext.models.azure import AzureAIChatCompletionClient
from autogen_ext.experimental.task_centric_memory import MemoryController
from autogen_ext.experimental.task_centric_memory.utils import Teachability
from azure.core.credentials import AzureKeyCredential

async def main():
    # Debugging information
    endpoint = os.getenv("AZURE_AI_CHAT_ENDPOINT") or ""
    api_key = os.getenv("AZURE_AI_CHAT_KEY") or ""
    model = os.getenv("CHAT_MODEL")
    print(f"Endpoint: {endpoint}")
    #print(f"API Key: {api_key}")
    print(f"Model: {model}")

    # Create a client
    client = AzureAIChatCompletionClient(
        endpoint=endpoint,
        credential=AzureKeyCredential(api_key),
        model=model,
        model_info={
            "json_output": True,
            "function_calling": True,
            "vision": True,
            "family": "unknown",
        }
    )

    # Create an instance of Task-Centric Memory, passing minimal parameters for this simple example
    memory_controller = MemoryController(reset=False, client=client)

    # Wrap the memory controller in a Teachability instance
    teachability = Teachability(memory_controller=memory_controller)

    # Create an AssistantAgent, and attach teachability as its memory
    assistant_agent = AssistantAgent(
        name="teachable_agent",
        system_message = "You are a helpful AI assistant, with the special ability to remember user teachings from prior conversations.",
        model_client=client,
        memory=[teachability],
    )

    # Enter a loop to chat with the teachable agent
    print("Now chatting with a teachable agent. Please enter your first message. Type 'exit' or 'quit' to quit.")
    while True:
        user_input = input("\nYou: ")
        if user_input.lower() in ["exit", "quit"]:
            break
        await Console(assistant_agent.run_stream(task=user_input))

    # Close the connection to the client
    await client.close()

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())
