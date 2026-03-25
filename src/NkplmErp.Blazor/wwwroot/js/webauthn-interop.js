window.webAuthnInterop = {
    register: async (optionsJson) => {
        console.log(">>>> [DEBUG] WebAuthn Register START", optionsJson);
        const options = JSON.parse(optionsJson);

        // Decode base64 strings to ArrayBuffers
        options.user.id = bufferDecode(options.user.id);
        options.challenge = bufferDecode(options.challenge);

        if (options.excludeCredentials) {
            for (let i = 0; i < options.excludeCredentials.length; i++) {
                options.excludeCredentials[i].id = bufferDecode(options.excludeCredentials[i].id);
            }
        }

        try {
            const credential = await navigator.credentials.create({
                publicKey: options
            });

            console.log(">>>> [DEBUG] WebAuthn Register SUCCESS", credential);
            return {
                id: credential.id,
                rawId: bufferEncode(credential.rawId),
                type: credential.type,
                extensions: credential.getClientExtensionResults(),
                response: {
                    attestationObject: bufferEncode(credential.response.attestationObject),
                    clientDataJSON: bufferEncode(credential.response.clientDataJSON),
                    transports: credential.response.getTransports ? credential.response.getTransports() : []
                }
            };
        } catch (err) {
            console.error(">>>> [DEBUG] WebAuthn Register ERROR", err);
            throw new Error(err.message || "Credential creation failed");
        }
    },
    login: async (optionsJson) => {
        console.log(">>>> [DEBUG] WebAuthn Login START", optionsJson);
        const options = JSON.parse(optionsJson);

        options.challenge = bufferDecode(options.challenge);

        if (options.allowCredentials) {
            for (let i = 0; i < options.allowCredentials.length; i++) {
                options.allowCredentials[i].id = bufferDecode(options.allowCredentials[i].id);
            }
        }

        try {
            const assertion = await navigator.credentials.get({
                publicKey: options
            });

            console.log(">>>> [DEBUG] WebAuthn Login SUCCESS", assertion);
            return {
                id: assertion.id,
                rawId: bufferEncode(assertion.rawId),
                type: assertion.type,
                extensions: assertion.getClientExtensionResults(),
                response: {
                    authenticatorData: bufferEncode(assertion.response.authenticatorData),
                    clientDataJSON: bufferEncode(assertion.response.clientDataJSON),
                    signature: bufferEncode(assertion.response.signature),
                    userHandle: assertion.response.userHandle ? bufferEncode(assertion.response.userHandle) : null
                }
            };
        } catch (err) {
            console.error(">>>> [DEBUG] WebAuthn Login ERROR", err);
            throw new Error(err.message || "Assertion retrieval failed");
        }
    }
};

function bufferDecode(value) {
    let s = value.replace(/-/g, "+").replace(/_/g, "/");
    while (s.length % 4) s += "=";
    return Uint8Array.from(atob(s), c => c.charCodeAt(0));
}

function bufferEncode(value) {
    return btoa(String.fromCharCode.apply(null, new Uint8Array(value)))
        .replace(/\+/g, "-")
        .replace(/\//g, "_")
        .replace(/=/g, "");
}
