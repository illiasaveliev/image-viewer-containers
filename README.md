# Overview

The goal of this workshop is to get familiar with **AWS Serverless Containers** stack by creating the **Image Viewer** application.

The result of the all steps will be a fully working web site, where you can upload images and search them by the image content (exactly persons and objects that present on a picture).

## Preparation

Current workshop requires several installed tools. The following preparation steps must be performed in the beginning

1. Check that **AWS CLI** is working. Open command line and execute the command below.

    ```sh
    aws s3 ls
    ```

    If **AWS CLI** is not working follow [install instructions](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html) and [configuration steps](https://docs.aws.amazon.com/cli/latest/userguide/cli-chap-configure.html) after installation.

2. **Docker** must be available, you can get it here <https://docs.docker.com/get-docker/>

## Workshop Structure

Workshop contains several parts. Please clone or download this repo and then follow instructions inside the each part and have fun

* [Part 1 – Create Image Viewer API](docs/part1/part.md)
* [Part 2 – Secure your application](docs/part2/part.md)
* [Part 3 – Deploy Image Viewer Web application](docs/part3/part.md)
* [Part 4 – Add Image Viewer labeling service](docs/part4/part.md)
* [Part 5 – Clean-up resources](docs/part5/part.md)
