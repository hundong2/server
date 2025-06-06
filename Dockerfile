# Use an official Keycloak image as a parent image
FROM quay.io/keycloak/keycloak:latest

# Set environment variables for Keycloak admin user
# These can be overridden at runtime by docker-compose
ENV KEYCLOAK_ADMIN=admin
ENV KEYCLOAK_ADMIN_PASSWORD=admin

# Expose the port Keycloak runs on
EXPOSE 8080

# Command to run Keycloak
CMD ["start-dev"]
