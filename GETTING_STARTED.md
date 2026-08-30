# Getting Started with BoardOil

This guide gets BoardOil running in the simplest way. For HTTPS, persistent data, and MCP configuration, see [Advanced Installation](ADVANCED_INSTALLATION.md).

## Install BoardOil

The quickest way to get going is with Docker Compose. Save the following as `compose.yml`:

```yaml
services:
  boardoil:
    image: dozigden/boardoil:latest
    container_name: boardoil
    ports:
      - "5000:5000"
    environment:
      BoardOilAuth__AllowInsecureCookies: "true"
    volumes:
      - ./boardoil-data:/data
    restart: unless-stopped
```

Start BoardOil:

```sh
docker compose up -d
```

Follow the startup logs:

```sh
docker compose logs -f boardoil
```

Once the container is ready, open `http://localhost:5000` in a browser. If BoardOil is installed on another computer, replace `localhost` accordingly.

The first visit opens the initial admin setup. You should do it straight away. This setup is available only while the installation has no users.

### Home Assistant

BoardOil has two community Home Assistant projects:

- To install BoardOil as a Home Assistant app, use [ha-boardoil app](https://github.com/bexelbie/ha-boardoil), built by [@bexelbie](https://github.com/bexelbie).
- To connect Home Assistant to BoardOil and automate card tasks, use the [HA-BoardOil integration](https://github.com/andrew-codechimp/HA-BoardOil), built by [@andrew-codechimp](https://github.com/andrew-codechimp).

## Next steps

This configuration serves BoardOil over plain HTTP and is suitable for localhost or a trusted private network. See [Advanced Installation](ADVANCED_INSTALLATION.md#https) for anything more complex.

The `boardoil-data` folder holds the database, uploaded images, and authentication signing key. Do not remove it when recreating or updating the container. See the [BoardOil FAQ](FAQ.md) for common questions.
