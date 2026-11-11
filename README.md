# Android potpourri

Demonstration of Turian+Android capabilities

## Build locally

Install the toolchain pinned in the engine's `ci/android-toolchain.env`, then run:

```sh
turian-cli brick restore --project .
turian-cli export . --profile Android --configuration Debug
```

The APK is written to `Output/Android/<game>/`. The `Android` profile is a Development build signed with the debug key.
