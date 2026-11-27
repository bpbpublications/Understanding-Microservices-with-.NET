terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "~> 4.0" }
  }
}
provider "azurerm" {
  features {}
}
variable "storage_name" {
  type        = string
  description = "A globally unique name with 3 to 24 lowercase letters and digits."
  validation {
    condition     = can(regex("^[a-z0-9]{3,24}$", var.storage_name))
    error_message = "Use 3 to 24 lowercase letters and digits."
  }
}
resource "azurerm_resource_group" "book" {
  name     = "book-solutions-lab"
  location = "westus2"
}
resource "azurerm_storage_account" "book" {
  name                            = var.storage_name
  resource_group_name             = azurerm_resource_group.book.name
  location                        = azurerm_resource_group.book.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
}
