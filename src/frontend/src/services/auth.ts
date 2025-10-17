/** TokenCredential compatible with core-http */
export const tokenCredential: TokenCredential = {
  getToken: async (): Promise<AccessToken | null> => {
    const authResponse = await getApiToken();
    if (!authResponse?.expiresOn) return null;
    return {
      token: authResponse.accessToken,
      expiresOnTimestamp: authResponse.expiresOn.getTime() / 1000,
    };
  },
};
