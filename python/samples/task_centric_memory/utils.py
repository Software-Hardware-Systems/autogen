from typing import Any, Dict
import yaml
import os

from autogen_core.models import (
    ChatCompletionClient,
)
from autogen_ext.models.openai import OpenAIChatCompletionClient
from autogen_ext.models.azure import AzureAIChatCompletionClient
from azure.core.credentials import AzureKeyCredential

def create_oai_client(config: Dict[str, Any]) -> ChatCompletionClient:
    """
    Creates a chat completion client from OpenAI.

    Args:
        config (Dict[str, Any]): Configuration dictionary with the following keys:
            - model (str): The model name.
            - max_completion_tokens (int): Maximum number of tokens for completion.
            - max_retries (int): Maximum number of retries for the request.
            - temperature (float): Sampling temperature.
            - presence_penalty (float): Presence penalty for the model.
            - frequency_penalty (float): Frequency penalty for the model.
            - top_p (float): Top-p sampling parameter.

    Returns:
        ChatCompletionClient: An instance of OpenAIChatCompletionClient.
    """
    client = OpenAIChatCompletionClient(
        model=config.get("model", "default_model"),
        max_tokens=config["max_completion_tokens"],
        max_retries=config["max_retries"],
        temperature=config["temperature"],
        presence_penalty=config["presence_penalty"],
        frequency_penalty=config["frequency_penalty"],
        top_p=config["top_p"],
    )
    return client

def create_azureai_client(config: Dict[str, Any]) -> ChatCompletionClient:
    """
    Creates a chat completion client for Azure AI.

    Args:
        config (Dict[str, Any]): Configuration dictionary with the following keys:
            - model (str): The model name.
            - max_completion_tokens (int): Maximum number of tokens for completion.
            - max_retries (int): Maximum number of retries for the request.
            - temperature (float): Sampling temperature.
            - presence_penalty (float): Presence penalty for the model.
            - frequency_penalty (float): Frequency penalty for the model.
            - top_p (float): Top-p sampling parameter.
            - endpoint (str): The Azure endpoint.
            - api_key (str): The API key for authentication.

    Returns:
        ChatCompletionClient: An instance of AzureAIChatCompletionClient.
    """
    # Debugging information
    api_key = os.getenv("AZURE_AI_CHAT_KEY") or config["api_key"]
    endpoint = os.getenv("AZURE_AI_CHAT_ENDPOINT") or config["endpoint"]

    # Create a client
    client = AzureAIChatCompletionClient(
        endpoint=endpoint,
        credential=AzureKeyCredential(api_key),
        model=config["model"],
        model_info=config.get("model_info", {
            "json_output": True,
            "function_calling": True,
            "vision": False,
            "family": "unknown",
        }),
        max_tokens=config["max_completion_tokens"],
        temperature=config["temperature"],
        presence_penalty=config["presence_penalty"],
        frequency_penalty=config["frequency_penalty"],
        top_p=config["top_p"],
    )
    return client

def load_yaml_file(file_path: str) -> Any:
    """
    Opens a file and returns its contents.
    """
    with open(file_path, "r") as file:
        return yaml.load(file, Loader=yaml.FullLoader)

