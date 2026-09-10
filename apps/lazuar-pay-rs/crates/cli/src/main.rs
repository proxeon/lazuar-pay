//! `lazuar-pay` entry. Host JSON to stdout (`--compact` / `--quiet`); problems to stderr.

use clap::Parser;
use pay_cli::{api_key_flag_on_argv, run, stdout_json, Cli};

#[tokio::main]
async fn main() {
    let cli = Cli::parse();
    let compact = cli.compact;
    let quiet = cli.quiet;
    // Env-only key is safer; `--quiet` also silences this hint.
    if !quiet && api_key_flag_on_argv(std::env::args()) {
        eprintln!(
            "lazuar-pay: prefer env LAZUAR_PAY_API_KEY (or PAY_API_KEY); --api-key is visible in ps/history"
        );
    }
    match run(cli).await {
        Ok(body) => match stdout_json(&body, compact, quiet) {
            Some(s) => println!("{s}"),
            None => {}
        },
        Err(e) => {
            // Agents parse problem+json (036/006 #8). Human Display is in `detail`.
            match serde_json::to_string(&e.to_json()) {
                Ok(s) => eprintln!("{s}"),
                Err(_) => eprintln!("{e}"),
            }
            // 0 ok · 1 4xx · 2 config · 3 auth · 4 404 · 5 5xx · 6 transport · 8 wait timeout.
            std::process::exit(e.exit_code());
        }
    }
}
