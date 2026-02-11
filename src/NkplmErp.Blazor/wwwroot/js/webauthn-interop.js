window.webAuthnInterop = {
    register: async (optionsJson) => {
        const options = JSON.parse(optionsJson);

        // Decode base64 strings to ArrayBuffers
        options.user.id = bufferDecode(options.user.id);
        options.challenge = bufferDecode(options.challenge);

        if (options.excludeCredentials) {
            for (let i = 0; i < options.excludeCredentials.length; i++) {
                options.excludeCredentials[i].id = bufferDecode(options.excludeCredentials[i].id);
            }
        }

        const credential = await navigator.credentials.create({
            publicKey: options
        });

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
    },
    login: async (optionsJson) => {
        const options = JSON.parse(optionsJson);

        options.challenge = bufferDecode(options.challenge);

        if (options.allowCredentials) {
            for (let i = 0; i < options.allowCredentials.length; i++) {
                options.allowCredentials[i].id = bufferDecode(options.allowCredentials[i].id);
            }
        }

        const assertion = await navigator.credentials.get({
            publicKey: options
        });

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
    }
};

function bufferDecode(value) {
    return Uint8Array.from(atob(value.replace(/-/g, "+").replace(/_/g, "/")), c => c.charCodeAt(0));
}

function bufferEncode(value) {
    return btoa(String.fromCharCode.apply(null, new Uint8Array(value)))
        .replace(/\+/g, "-")
        .replace(/\//g, "_")
        .replace(/=/g, "");
}
