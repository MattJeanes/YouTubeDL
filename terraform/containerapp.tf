resource "azurerm_container_app_environment" "main" {
  name                       = "youtubedl-env"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
}

resource "azurerm_container_app" "main" {
  name                         = "youtubedl"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"

  template {
    container {
      name   = "youtubedl-web"
      image  = var.image_sha != "" ? "ghcr.io/mattjeanes/youtubedl/youtubedl.web@${var.image_sha}" : "ghcr.io/mattjeanes/youtubedl/youtubedl.web:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"
      env {
        name        = "ApiKey"
        secret_name = "api-key"
      }
    }
  }

  secret {
    name  = "api-key"
    value = var.youtube_api_key
  }

  ingress {
    external_enabled = true
    transport        = "auto"
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

resource "azurerm_container_app_custom_domain" "main" {
  lifecycle {
    ignore_changes = [certificate_binding_type, container_app_environment_certificate_id]
  }

  name             = cloudflare_dns_record.main.name
  container_app_id = azurerm_container_app.main.id

  depends_on = [
    cloudflare_dns_record.main_validation
  ]
}
