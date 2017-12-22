# Unity-Network-Audio-Source
The Network AudioSource allows for easy and fast synchronisation of AudioSources across the network using Unity‘s NetworkMessages. It synchronises from and to both clients and servers.

It additionally supports the features
- FadeIn and FadeOut, which allow for linear fading of a currently played AudioClip.
- Selecting random clips in a loop
- Linking NetworkAudioSources for complex AudioSource chains, excellent when transferring dampened sound in complex separated 2D rooms on a single scene

Prerequisites
Every AudioClip must have a unique name.
A connection must have been established using Unity‘s NetworkManager.