export default {
  '/api/**': {
    target: process.env['TIH_API_TARGET'] || 'https://localhost:7115',
    secure: false,
    changeOrigin: true,
  },
};
