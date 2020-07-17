# Part 5 – Clean-up resources

With a help of **CloudFormation** we can clean all the created resources within a few minutes.

1. Open **AWS Console**
2. Go to the **CloudFormation** service
3. Delete all created stacks
    - **image-viewer-api-containers**
    - **image-viewer-web-app**
    - **image-viewer-labeling-containers**
4. Go to the **AWS ECS** service and check that all resources were deleted, if no please delete them manually
5. Go to the **Cognito User Pools** and delete **image-cognito-pool**
6. Go to the **S3** and check that all test buckets were removed.
