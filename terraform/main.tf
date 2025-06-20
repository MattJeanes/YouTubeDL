terraform {
  backend "azurerm" {}
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "4.33.0"
    }
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "5.6.0"
    }
  }
}

provider "azurerm" {
  subscription_id = var.azure_subscription_id
  features {}
}

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}
