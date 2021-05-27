# Encrypted Chat
This is a very, very basic chat app using end-to-end encryption. 
The client is a basic WPF application without any specific architecture or best practices used.
The server is a basic server without any specific architecture used. 

## Communication Protocol
TCP with SSL is being used for communication between the client and server.

## Encryption

On the client, a private and public key pair is created for a user and stored locally as a key container in Windows.
The public key is shared to all users in the same chat room.

When sending a message, the message is encrypted with a symmetric key. The symmetric key is then encrypted using the public key of each user.
This encrypted message is sent through the server to each client. 
When the client receives the message, it uses the user's private key to decrypt symmetric key to then decrypt the message.