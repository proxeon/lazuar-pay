export const en = {
  "chrome.poweredBy": "Powered by Lazuar",
  "chrome.langEn": "EN",
  "chrome.langBm": "BM",
  "chrome.langSwitch": "Language",
  "footer.copyright": "© {year} Lazuar Platform. All rights reserved.",
  "footer.terms": "Terms",
  "footer.privacy": "Privacy",
  "footer.refund": "Refund Policy",
  "meta.title": "Lazuar Portal",
  "meta.description": "Secure checkout and buyer dashboard",
  "meta.checkoutTitle": "Checkout · {product}",

  "form.accountDetails": "Account Details",
  "form.fullName": "Full Name",
  "form.email": "Email Address",
  "form.phone": "WhatsApp Number",
  "form.phoneHint": "Required for delivery and important updates.",
  "form.phonePlaceholder": "+60 12-345 6789",
  "form.billingDetails": "Billing Details",
  "form.companyName": "Company Name *",
  "form.taxId": "Tax Identification Number (TIN) *",
  "form.taxIdHint": "Required for a Malaysian tax invoice. We will validate this number in a later step.",
  "form.taxIdPlaceholder": "e.g. C12345678",
  "form.billingAddress": "Billing Address *",
  "form.street": "Street Address",
  "form.city": "City",
  "form.postal": "Postal Code",
  "form.state": "State",
  "form.country": "Country Code (e.g. MY)",
  "form.consent":
    "By proceeding, you agree to Lazuar's {terms} and {privacy}, and acknowledge that your purchase is a direct transaction with {seller}.",
  "form.consentTerms": "Terms of Service",
  "form.consentPrivacy": "Privacy Policy",
  "cta.proceed": "Proceed to Payment",
  "cta.securing": "Securing Data...",

  "id.guest": "Checking out as Guest",
  "id.useAccount": "Use my Lazuar account",
  "id.admin": "Viewing as Workspace Admin",
  "id.asGuest": "Checkout as Guest",
  "id.loggedIn": "Logged in as {name}",

  "summary.title": "Order Summary",
  "summary.subtotal": "Subtotal",
  "summary.discount": "Discount",
  "summary.total": "Total Due Today",
  "summary.quantity": "Quantity",
  "summary.unitTimesQty": "{amount} × {n}",
  "summary.discountEach": "Discount (per item × {n})",
  "summary.thenRecurring": "then {amount} / {interval}",
  "summary.trialThen": "{days}-day trial, then {amount} / {interval}. Cancel anytime during trial.",
  "summary.intervalMonth": "month",
  "summary.intervalYear": "year",
  "summary.notAutoDebit": "Not auto-debit. We email a new payment link each cycle.",
  "summary.cardSaved": "Your card will be saved for renewals.",
  "summary.decreaseQty": "Decrease quantity",
  "summary.increaseQty": "Increase quantity",
  "promo.label": "Promo Code",
  "promo.placeholder": "ENTER CODE",
  "promo.apply": "Apply",
  "promo.remove": "Remove",

  "banner.cancelled":
    "Payment was cancelled or failed. Please try again or use a different payment method.",
  "error.generic": "An error occurred during checkout.",
  "error.invalidPromo": "Invalid promo code.",
  "error.promoNotApplicable": "This code cannot be applied.",
  "error.gatewayDown":
    "This creator is currently updating their payment settings. Please try again later.",
  "error.missingConfirmUrl":
    "Checkout completed but the confirmation link was missing. Please check your email.",
  "error.submitFailed": "Checkout submission failed.",
  "error.statusFailed": "Status check failed.",

  "success.invalidTitle": "Invalid Session",
  "success.invalidBody":
    "We could not verify your session. Please check your email for access links or contact support if you completed a payment.",
  "success.verifyingTitle": "Verifying Transaction...",
  "success.verifyingBody":
    "Please wait while we securely verify your transaction with the payment provider.",
  "success.expiredTitle": "Checkout Expired",
  "success.expiredBody":
    "This checkout session for {product} is no longer active. If you completed a payment, please check your email. Otherwise, start checkout again.",
  "success.returnCheckout": "Return to Checkout",
  "success.timeoutTitle": "Processing Payment",
  "success.timeoutBody":
    "We are still processing your payment for {product}. Please check your email in a few minutes for your receipt. This page does not confirm payment until verification finishes.",
  "success.checkAgain": "Check again",
  "success.dashboard": "Go to Dashboard",
  "success.completeTitle": "Order Complete!",
  "success.completeBody":
    "Your order for {product} is confirmed. Please check your email for your receipt.",

  "notFound.title": "Resource Not Found",
  "notFound.body":
    "The checkout page or portal you are looking for does not exist, has been archived, or the link has expired.",
  "notFound.home": "Return Home",
} as const;

export type MessageKey = keyof typeof en;

export const ms: Record<MessageKey, string> = {
  "chrome.poweredBy": "Dikuasakan oleh Lazuar",
  "chrome.langEn": "EN",
  "chrome.langBm": "BM",
  "chrome.langSwitch": "Bahasa",
  "footer.copyright": "© {year} Lazuar Platform. Hak cipta terpelihara.",
  "footer.terms": "Terma",
  "footer.privacy": "Privasi",
  "footer.refund": "Dasar bayaran balik",
  "meta.title": "Portal Lazuar",
  "meta.description": "Checkout selamat dan papan pemuka pembeli",
  "meta.checkoutTitle": "Bayar · {product}",

  "form.accountDetails": "Butiran akaun",
  "form.fullName": "Nama penuh",
  "form.email": "Alamat e-mel",
  "form.phone": "Nombor WhatsApp",
  "form.phoneHint": "Diperlukan untuk penghantaran dan maklumat penting.",
  "form.phonePlaceholder": "+60 12-345 6789",
  "form.billingDetails": "Butiran bil",
  "form.companyName": "Nama syarikat *",
  "form.taxId": "Nombor Pengenalan Cukai (TIN) *",
  "form.taxIdHint": "Diperlukan untuk invois cukai Malaysia. Kami akan mengesahkan nombor ini pada langkah kemudian.",
  "form.taxIdPlaceholder": "cth. C12345678",
  "form.billingAddress": "Alamat bil *",
  "form.street": "Alamat jalan",
  "form.city": "Bandar",
  "form.postal": "Poskod",
  "form.state": "Negeri",
  "form.country": "Kod negara (cth. MY)",
  "form.consent":
    "Dengan meneruskan, anda bersetuju dengan {terms} dan {privacy} Lazuar, dan mengakui bahawa pembelian ini ialah transaksi terus dengan {seller}.",
  "form.consentTerms": "Terma Perkhidmatan",
  "form.consentPrivacy": "Dasar Privasi",
  "cta.proceed": "Teruskan ke Pembayaran",
  "cta.securing": "Menyediakan pembayaran…",

  "id.guest": "Membayar sebagai tetamu",
  "id.useAccount": "Guna akaun Lazuar saya",
  "id.admin": "Melihat sebagai pentadbir ruang kerja",
  "id.asGuest": "Checkout sebagai tetamu",
  "id.loggedIn": "Log masuk sebagai {name}",

  "summary.title": "Ringkasan pesanan",
  "summary.subtotal": "Jumlah kecil",
  "summary.discount": "Diskaun",
  "summary.total": "Jumlah perlu dibayar hari ini",
  "summary.quantity": "Kuantiti",
  "summary.unitTimesQty": "{amount} × {n}",
  "summary.discountEach": "Diskaun (setiap item × {n})",
  "summary.thenRecurring": "kemudian {amount} / {interval}",
  "summary.trialThen": "Percubaan {days} hari, kemudian {amount} / {interval}. Boleh batal semasa percubaan.",
  "summary.intervalMonth": "bulan",
  "summary.intervalYear": "tahun",
  "summary.notAutoDebit":
    "Bukan debit automatik. Kami e-mel pautan pembayaran baharu setiap kitaran.",
  "summary.cardSaved": "Kad anda akan disimpan untuk pembaharuan.",
  "summary.decreaseQty": "Kurangkan kuantiti",
  "summary.increaseQty": "Tambah kuantiti",
  "promo.label": "Kod promo",
  "promo.placeholder": "MASUKKAN KOD",
  "promo.apply": "Guna",
  "promo.remove": "Buang",

  "banner.cancelled":
    "Pembayaran dibatalkan atau gagal. Sila cuba lagi atau guna kaedah pembayaran lain.",
  "error.generic": "Ralat berlaku semasa checkout.",
  "error.invalidPromo": "Kod promo tidak sah.",
  "error.promoNotApplicable": "Kod ini tidak boleh digunakan.",
  "error.gatewayDown":
    "Peniaga ini sedang mengemas kini tetapan pembayaran. Sila cuba lagi kemudian.",
  "error.missingConfirmUrl":
    "Checkout selesai tetapi pautan pengesahan tiada. Sila semak e-mel anda.",
  "error.submitFailed": "Checkout gagal dihantar.",
  "error.statusFailed": "Semakan status gagal.",

  "success.invalidTitle": "Sesi tidak sah",
  "success.invalidBody":
    "Kami tidak dapat mengesahkan sesi anda. Sila semak e-mel untuk pautan akses, atau hubungi sokongan jika anda sudah membayar.",
  "success.verifyingTitle": "Mengesahkan transaksi…",
  "success.verifyingBody":
    "Sila tunggu sementara kami mengesahkan transaksi anda dengan pembekal pembayaran.",
  "success.expiredTitle": "Checkout tamat tempoh",
  "success.expiredBody":
    "Sesi checkout untuk {product} tidak lagi aktif. Jika anda sudah membayar, sila semak e-mel. Jika tidak, mulakan checkout semula.",
  "success.returnCheckout": "Kembali ke checkout",
  "success.timeoutTitle": "Pembayaran sedang diproses",
  "success.timeoutBody":
    "Kami masih memproses pembayaran untuk {product}. Sila semak e-mel anda sebentar lagi untuk resit. Halaman ini tidak mengesahkan pembayaran sehingga pengesahan selesai.",
  "success.checkAgain": "Semak semula",
  "success.dashboard": "Pergi ke papan pemuka",
  "success.completeTitle": "Pesanan selesai!",
  "success.completeBody":
    "Pesanan anda untuk {product} telah disahkan. Sila semak e-mel untuk resit.",

  "notFound.title": "Sumber tidak dijumpai",
  "notFound.body":
    "Halaman checkout atau portal ini tidak wujud, telah diarkibkan, atau pautan telah tamat tempoh.",
  "notFound.home": "Kembali ke laman utama",
};

export const messages = {
  en,
  ms,
};
