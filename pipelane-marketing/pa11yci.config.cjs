module.exports = {
  defaults: {
    standard: "WCAG2AA",
    chromeLaunchConfig: {
      args: ["--no-sandbox", "--disable-setuid-sandbox", "--headless=new"]
    },
    timeout: 60000,
    log: {
      level: "error"
    }
  },
  urls: [
    "http://127.0.0.1:4321/",
    "http://127.0.0.1:4321/#product",
    "http://127.0.0.1:4321/#features",
    "http://127.0.0.1:4321/#integrations",
    "http://127.0.0.1:4321/#pricing",
    "http://127.0.0.1:4321/#faq"
  ]
};
