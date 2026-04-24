# Whale's Exchange Backend

This repository contains the official Whale's Exchange Backend App used by the [whales.exchange](https://whales.exchange) [frontend application](https://github.com/AITIS-s-r-o/whales-exchange-web-app) to facilitate communication with the Electrum Swap server. The backend is built using .NET 10 and is designed to be a minimalistic bridge between the frontend and the Electrum Swap server.

The complete Whale's Exchange design overview is shown in the GitHub [repository](https://github.com/AITIS-s-r-o/whales-exchange-web-app) of the frontend application.

## Contributing

We welcome contributions to the Whale's Exchange! If you have an idea for a new feature, improvement, or bug fix, please submit a pull request. For major changes, please open an issue first to discuss what you would like to change.

To run the backend app locally from source, you need to:


1. Install .NET SDK 10.0 using any [available method](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
1. Create `config.secret.json`, `config.secret.Debug.json`, and `config.secret.Release.json` files in the root of the project with the following content:
    ```json
    {
        "ConnectionString": "Data Source=wex.db",
        "ElectrumRpc": {
           "Uri": "http://localhost:7777",
           "User": "user",
           "Pass": "pass"
        }
    }
    ```
    See also [config.secret.json.template](https://github.com/AITIS-s-r-o/whales-exchange-backend/blob/master/config.secret.json.template).
1. Start the app:
    ```bash
    dotnet run
    ```

or use your favorite IDE to run the project. The app can be built and run on Linux and Windows. The project _should_ run on macOS but it is not actively tested.

## Resources

- Get Help: [Support Center](https://t.me/whales_secret_support)
- Follow us: [X/Twitter](https://x.com/WhalesSecret)
