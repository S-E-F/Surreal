# 30/01/2023 SSL for local development

I wanted to test the websocket connection over `wss://` in addition to `ws://`, so I had to figure out how to start surreal using an SSL certificate.

If you start surreal through the CLI there are two options that relate to SSL certificates:
- `--web-key`, the private key file
- `--web-crt`, the certificate file

Handling SSL certificates for localhost in the .NET landscape is generally handled through `dotnet dev-certs`, but this doesn't expose the file locations by default.

> `dotnet dev-certs https --export-path ./surreal.pem --format pem -np --trust`

This command generates two files (in the current directory):
- `surreal.pem`, the certificate file
- `surreal.key`, the private (unencrypted) key file

Note that the key must be unencrypted or surreal won't be able to read the file (it doesn't support passing the password in).

The certificate file will work for surreal, but the key file won't.
By default, `dotnet dev-certs` will generate a PKCS#12 format key, which surreal doesn't understand.

Luckily the error tells us that surreal expects PKCS8, so to convert the key format use
> ```
> openssl pkcs12 -export -in surreal.pem -inkey surreal.key -out surreal.pfx
> openssl pkcs12 -in surreal.pfx -nocerts -nodes -out tmp.pem
> openssl pkcs8 -in tmp.pem -topk8 -nocrypt -out surreal.pk8
> rm tmp.pem
> ```

I ran `openssl` on Windows in Git Bash, as each installation of git comes with an installation of openssl. The location of openssl is not part of the global PATH, but it is accessible from git bash.

This will add `surreal.pk8` which we can use when starting surreal:

> `surreal start --user root --pass root --web-key surreal.pk8 --web-crt surreal.pem memory`

Client-side, .NET handles the client certificate for us as long as we properly generated the initial certs using `dotnet dev-certs`.

# Disclaimer
I am by no means an expert on security so take this document with a grain of salt and of course, as with all local development certificates, DO NOT USE THIS IN PRODUCTION.